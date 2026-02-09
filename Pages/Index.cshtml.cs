using AS_Assignment2.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AS_Assignment2.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        public Member? Member { get; set; }
        public string? DecryptedCreditCard { get; set; }

        public IndexModel(AppDbContext db, IDataProtectionProvider dataProtectionProvider)
        {
            _db = db;
            _dataProtectionProvider = dataProtectionProvider;
        }

        public void OnGet()
        {
            // Landing page - redirect users to login or register
        }
    }
}
