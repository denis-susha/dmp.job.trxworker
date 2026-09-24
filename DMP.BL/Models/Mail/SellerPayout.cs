namespace DMP.BL.Models.Mail;

// Serialized into Mail.Model and rendered by the email template, so property names are part of the template contract.
public class SellerPayout
{
    public string ButtonLink { get; set; } = null!;
    public string LogoUrl { get; set; } = null!;
    public string Year { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Amount { get; set; } = null!;
    public string NetworkFee { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string? NetworkTransaction { get; set; }
}
