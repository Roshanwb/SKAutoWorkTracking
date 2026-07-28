namespace SKAuto.Core.DTOs
{
    public class AppConfig
    {
        public string Language { get; set; } = "fr-FR";
        public ReportColors ReportColors { get; set; } = new();
        public ReportFonts ReportFonts { get; set; } = new();
        public int MaxBackupsToKeep { get; set; } = 20;   // Number of backups to keep
                                                          // NEW: Email settings for password reset
        public EmailConfig Email { get; set; } = new();
        public string Theme { get; set; } = "Light";
    }

    public class EmailConfig
    {
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "dev.skauto@gmail.com";
        public string SenderPassword { get; set; } = "wwua toth kdgs masi";
        public string SenderName { get; set; } = "SKAuto Work Tracking";
    }

    public class ReportColors
    {
        public string HeaderBackground { get; set; } = "#2C3E50";
        public string HeaderForeground { get; set; } = "#FFFFFF";
        public string AlternateRowBackground { get; set; } = "#F0F0F0";
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