using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Infrastructure.Notifications;
using JellyFish.Api.Infrastructure.Persistence;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Auditing;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace JellyFish.Api.Tests.Features.JellyfishController.Commands;

public class RunPatternCommandHandlerTests
{
    [Fact]
    public async Task On_without_ScheduleName_uses_default_schedule_priority_and_upserts_activezones()
    {
        // Arrange
        var client = new Mock<IJellyfishControllerClient>();
        client.Setup(c => c.RunPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<PatternDetails?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Success());

        var overrideSvc = new Mock<IManualOverrideService>();
        var schedules = new Mock<IScheduleRepository>();
        var zones = new Mock<IZoneRepository>();
        var nextSvc = new Mock<IScheduleNextEventService>();
        var logger = new Mock<ILogger<RunPatternCommandHandler>>();
        var events = new Mock<IStateChangeBroadcaster>();
        var activeRepo = new Mock<IActiveZoneRepository>();
        var auditLog = new Mock<IAuditLogService>();
        auditLog.Setup(a => a.LogActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new RunPatternCommandHandler(client.Object, overrideSvc.Object, schedules.Object, zones.Object, nextSvc.Object, logger.Object, events.Object, activeRepo.Object, auditLog.Object, uow.Object);

        var cmd = new RunPatternCommand(
            FolderName: "Holiday",
            Name: "RedGreen",
            State: 1,
            ZoneNames: new List<string> { "Front", "Back" },
            Data: null,
            IsManual: false,
            ScheduleName: null
        );

        // Act
        var res = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        res.IsSuccess.ShouldBeTrue();
        activeRepo.Verify(r => r.UpsertAsync(It.Is<JellyFish.Api.Domain.ActiveZone>(az => az.ZoneName == "Front" && az.FolderName == "Holiday" && az.PatternName == "RedGreen" && az.Priority == 5), It.IsAny<CancellationToken>()), Times.Once);
        activeRepo.Verify(r => r.UpsertAsync(It.Is<JellyFish.Api.Domain.ActiveZone>(az => az.ZoneName == "Back" && az.Priority == 5), It.IsAny<CancellationToken>()), Times.Once);
    }
}

