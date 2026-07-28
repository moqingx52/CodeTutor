from fastapi import FastAPI, File, Form, UploadFile
from pydantic import BaseModel

app = FastAPI(title="CodeTutor OCR Sidecar", version="0.1.0")


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


@app.get("/healthz", response_model=HealthResponse)
async def healthz() -> HealthResponse:
    return HealthResponse(
        status="ok",
        engine="rapidocr",
        device="cpu",
        model="ch_en",
        version="0.1.0",
    )


@app.post("/v1/ocr", response_model=OcrResponse)
async def ocr(
    image: UploadFile = File(...),
    profile: str = Form("screen-default"),
    language: str = Form("ch_en"),
    request_id: str | None = Form(None),
) -> OcrResponse:
    # TODO (Agent): integrate rapidocr + onnxruntime CPU inference
    _ = (image, profile, language)
    return OcrResponse(
        requestId=request_id or "stub",
        width=1280,
        height=720,
        elapsedMs=0,
        meanConfidence=0.0,
        fullText="",
        lines=[],
    )
