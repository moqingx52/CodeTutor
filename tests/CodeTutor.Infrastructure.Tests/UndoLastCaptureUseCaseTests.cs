using CodeTutor.Application.Abstractions;
using CodeTutor.Application.State;
using CodeTutor.Application.UseCases;
using CodeTutor.Domain.Ocr;
using CodeTutor.Domain.Sessions;
using FluentAssertions;
using NSubstitute;

namespace CodeTutor.Infrastructure.Tests;

public class UndoLastCaptureUseCaseTests
{
    [Fact]
    public async Task Execute_RestoresCheckpointAndRemovesCapture()
    {
        var sessionId = Guid.NewGuid();
        var session = new StudySession(
            sessionId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            SessionStatus.Active,
            "合并后题干",
            false,
            [
                new CaptureRecord(
                    Guid.NewGuid(),
                    sessionId,
                    1,
                    DateTimeOffset.UtcNow,
                    "/tmp/capture_001.png",
                    "/tmp/thumb.jpg",
                    "hash1",
                    OcrStatus.Succeeded,
                    null,
                    null,
                    null)
            ],
            null,
            []);

        var context = Substitute.For<IAppSessionContext>();
        context.Current.Returns(session);

        var checkpoints = Substitute.For<ICheckpointStore>();
        checkpoints.PopAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new SessionCheckpoint(
                sessionId,
                0,
                string.Empty,
                false,
                null,
                [],
                DateTimeOffset.UtcNow));

        var imageStore = Substitute.For<IImageStore>();
        var useCase = new UndoLastCaptureUseCase(context, checkpoints, imageStore);

        await useCase.ExecuteAsync(CancellationToken.None);

        await imageStore.Received(1).MoveToTrashAsync("/tmp/capture_001.png", Arg.Any<CancellationToken>());
        context.Received(1).ApplySession(Arg.Is<StudySession>(s =>
            s.Captures.Count == 0 && s.WorkingQuestionText == string.Empty));
        await context.Received(1).PersistCurrentAsync(Arg.Any<CancellationToken>());
    }
}
