using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.Models;

public class EmailTemplateDAL
{
    public int EmailTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!; // HTML with Razor syntax
    public Language Language { get; set; }

    public virtual ICollection<MailDAL>? Mails { get; set; }
}
