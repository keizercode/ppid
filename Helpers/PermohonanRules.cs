using PermintaanData.Models;

namespace PermintaanData.Helpers;

public static class PermohonanRules
{
    public const string PpidLsmOnlineUrl = "https://ppid.jakarta.go.id/permohonan-informasi";

    public static bool IsLsm(PermohonanPPID p) =>
        p.LoketJenis == LoketJenis.Umum
        || (!string.IsNullOrEmpty(p.KategoriPemohon) && p.KategoriPemohon != "Mahasiswa");

    public static bool IsLsm(string? kategori, string? loketJenis) =>
        loketJenis == LoketJenis.Umum
        || (!string.IsNullOrEmpty(kategori) && kategori != "Mahasiswa");

    /// <summary>Menghapus awalan "Kepala " untuk kalimat hubungi (Bidang / Suku Dinas).</summary>
    public static string StripKepalaPrefix(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return label;
        return label.StartsWith("Kepala ", StringComparison.OrdinalIgnoreCase)
            ? label[7..].Trim()
            : label.Trim();
    }

    /// <summary>Format unit untuk kalimat "agar menghubungi …".</summary>
    public static string FormatUnitHubungi(string disposisiLabel)
    {
        var unit = StripKepalaPrefix(disposisiLabel);
        if (unit.StartsWith("Suku Dinas", StringComparison.OrdinalIgnoreCase))
            return $"{unit} Lingkungan Hidup";
        if (unit.StartsWith("Bidang ", StringComparison.OrdinalIgnoreCase))
            return $"{unit} Dinas Lingkungan Hidup Provinsi DKI Jakarta";
        if (unit.StartsWith("Unit ", StringComparison.OrdinalIgnoreCase)
         || unit.StartsWith("Laboratorium", StringComparison.OrdinalIgnoreCase))
            return $"{unit} Dinas Lingkungan Hidup Provinsi DKI Jakarta";
        return $"{unit} Dinas Lingkungan Hidup Provinsi DKI Jakarta";
    }

    public static IEnumerable<string> ParentDisposisiLabels(IEnumerable<string> bidangTujuan)
    {
        return bidangTujuan
            .Select(b => b.Contains(" — ")
                ? b.Split(new[] { " — " }, 2, StringSplitOptions.None)[0].Trim()
                : b.Trim())
            .Where(b => b.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

public static class SumberRegistrasi
{
    public const string Online  = "Online";
    public const string Offline = "Offline";
}

public enum RekapBulananScope
{
    LoketKepegawaian,
    LoketUmum,
    KasubkelKepegawaian,
    KasubkelKdi
}
