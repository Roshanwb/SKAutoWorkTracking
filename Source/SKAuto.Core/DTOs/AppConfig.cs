namespace SKAuto.Core.DTOs
{
    public class AppConfig
    {
        public string Language { get; set; } = "fr-FR";
        public ReportColors ReportColors { get; set; } = new();
        public ReportFonts ReportFonts { get; set; } = new();
    }

    public class ReportColors
    {
        public string HeaderBackground { get; set; } = "#2C3E50";
        public string HeaderForeground { get; set; } = "#FFFFFF";
        public string AlternateRowBackground { get; set; } = "#F5F5F5";
        public string Border { get; set; } = "#DDDDDD";
        public string TotalBackground { get; set; } = "#E6FFE6";
        public string Accent { get; set; } = "#27AE60";
    }

    public class ReportFonts
    {
        public string FontFamily { get; set; } = "Helvetica";
        public int HeaderSize { get; set; } = 10;
        public int NormalSize { get; set; } = 9;
        public int TitleSize { get; set; } = 20;
    }
}