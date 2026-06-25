using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

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

    /// <summary>LSM hanya boleh memilih keperluan Permintaan Data.</summary>
    public static void ApplyLsmKeperluanOnly(DaftarPemohonVm vm)
    {
        if (!IsLsm(vm.Kategori, vm.LoketJenis)) return;
        vm.IsPermintaanData = true;
        vm.IsObservasi      = false;
        vm.IsWawancara      = false;
    }

    /// <summary>Menghapus awalan "Kepala " untuk kalimat hubungi (Bidang / Suku Dinas).</summary>
    public static string StripKepalaPrefix(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return label;
        return label.StartsWith("Kepala ", StringComparison.OrdinalIgnoreCase)
            ? label[7..].Trim()
            : label.Trim();
    }

    /// <summary>Format unit untuk kalimat "agar menghubungi …" (tanpa awalan Kepala).</summary>
    public static string FormatUnitHubungi(string disposisiLabel)
    {
        var unit = StripKepalaPrefix(disposisiLabel);
        if (unit.StartsWith("Suku Dinas", StringComparison.OrdinalIgnoreCase))
        {
            if (unit.Contains("Lingkungan Hidup", StringComparison.OrdinalIgnoreCase))
                return unit;
            return $"{unit} Lingkungan Hidup";
        }
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

    /// <summary>Label jenis kegiatan surat izin dari flag keperluan permohonan.</summary>
    public static string BuildJenisKegiatanLabel(PermohonanPPID p) =>
        BuildJenisKegiatanLabel(p.IsPermintaanData, p.IsObservasi, p.IsWawancara);

    public static string BuildJenisKegiatanLabel(bool isPermintaanData, bool isObservasi, bool isWawancara)
    {
        if (isPermintaanData && isObservasi && isWawancara)
            return "Observasi, Permintaan Data, dan Wawancara";
        if (isPermintaanData && isObservasi)
            return "Observasi dan Permintaan Data";
        if (isPermintaanData && isWawancara)
            return "Permintaan Data dan Wawancara";
        if (isObservasi && isWawancara)
            return "Observasi dan Wawancara";
        if (isPermintaanData) return "Permintaan Data";
        if (isObservasi) return "Observasi";
        if (isWawancara) return "Wawancara";
        return "Permintaan Data";
    }

    /// <summary>Opsi jenis kegiatan yang dapat dipilih petugas (default dari keperluan permohonan).</summary>
    public static readonly string[] JenisKegiatanOptions =
    [
        "Permintaan Data",
        "Observasi",
        "Wawancara",
        "Observasi dan Permintaan Data",
        "Permintaan Data dan Wawancara",
        "Observasi dan Wawancara",
        "Observasi, Permintaan Data, dan Wawancara"
    ];
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
