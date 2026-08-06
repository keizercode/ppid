using PermintaanData.Domain.Entities;

namespace PermintaanData.Domain.Repositories
{
    public interface IPPID
    {
        IQueryable<Pribadi> Pribadis { get; }
        IQueryable<PribadiPPID> PribadiPPIDs { get; }
        IQueryable<PermohonanPPID> PermohonanPPIDs { get; }
        IQueryable<PermohonanPPIDDetail> PermohonanPPIDDetails { get; }
        IQueryable<Keperluan> Keperluans { get; }
        IQueryable<StatusPPID> StatusPPIDs { get; }
        IQueryable<DokumenPPID> DokumenPPIDs { get; }
        IQueryable<JenisDokumenPPID> JenisDokumenPPIDs { get; }
        IQueryable<JadwalPPID> JadwalPPIDs { get; }
        IQueryable<AuditLogPPID> AuditLogPPIDs { get; }
        IQueryable<SubTaskPPID> SubTaskPPIDs { get; }
        IQueryable<FeedbackTaskPPID> FeedbackTaskPPIDs { get; }
        IQueryable<AppUser> AppUsers { get; }
    }
}
