namespace Asterisk.Events.Worker.Models.Events;
internal sealed record CompanyResumeVm(
  Dictionary<string, InBoundResume> In,
  OutBoundResume Out
);
