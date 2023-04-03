using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatBot.Models
{
    public class UserProfile
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CleaningTime { get; set; }

        public string PhoneNumber { get; set; }

        public string Clean { get; set; }
    }
}
