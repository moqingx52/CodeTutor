import io
import time
import uuid
from functools import lru_cache

import cv2
import numpy as np
from fastapi import FastAPI, File, Form, UploadFile
from pydantic import BaseModel
from rapidocr_onnxruntime import RapidOCR

app = FastAPI(title="CodeTutor OCR 旁路服务", version="0.2.0")


class HealthResponse(BaseModel):
    status: str
    engine: str
    device: str
    model: str
    version: str


class OcrLineResponse(BaseModel):
    text: str
    confidence: float
    polygon: list[list[float]]


class OcrResponse(BaseModel):
    requestId: str
    width: int
    height: int
    elapsedMs: int
    meanConfidence: float
    fullText: str
    lines: list[OcrLineResponse]


@lru_cache(maxsize=1)
def get_engine() -> RapidOCR:
    return RapidOCR()


def _decode_image(data: bytes) -> np.ndarray:
    arr = np.frombuffer(data, dtype=np.uint8)
    image = cv2.imdecode(arr, cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError("无法解码图片，请上传 PNG 或 JPEG 格式。")
    return image


def _preprocess(image: np.ndarray, profile: str) -> np.ndarray:
    height, width = image.shape[:2]
    long_edge = max(width, height)
    if long_edge < 1280:
        scale = min(2.0, 1280 / long_edge)
        image = cv2.resize(
            image,
            (int(width * scale), int(height * scale)),
            interpolation=cv2.INTER_CUBIC,
        )

    if profile == "screen-dark":
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        return cv2.bitwise_not(gray)

    return image


@app.get("/healthz", response_model=HealthResponse)
async def healthz() -> HealthResponse:
    return HealthResponse(
        status="ok",
        engine="rapidocr",
        device="cpu",
        model="ch_en",
        version="0.2.0",
    )


@app.post("/v1/ocr", response_model=OcrResponse)
async def ocr(
    image: UploadFile = File(...),
    profile: str = Form("screen-default"),
    language: str = Form("ch_en"),
    request_id: str | None = Form(None),
) -> OcrResponse:
    _ = language
    started = time.perf_counter()
    raw = await image.read()
    decoded = _decode_image(raw)
    processed = _preprocess(decoded, profile)

    engine = get_engine()
    result, _ = engine(processed)
    elapsed_ms = int((time.perf_counter() - started) * 1000)

    lines: list[OcrLineResponse] = []
    if result:
        for item in result:
            box, text, score = item[0], item[1], float(item[2])
            polygon = [[float(p[0]), float(p[1])] for p in box]
            lines.append(OcrLineResponse(text=text, confidence=score, polygon=polygon))

    full_text = "\n".join(line.text for line in lines)
    mean_conf = sum(line.confidence for line in lines) / len(lines) if lines else 0.0
    height, width = decoded.shape[:2]

    return OcrResponse(
        requestId=request_id or uuid.uuid4().hex,
        width=width,
        height=height,
        elapsedMs=elapsed_ms,
        meanConfidence=mean_conf,
        fullText=full_text,
        lines=lines,
    )
