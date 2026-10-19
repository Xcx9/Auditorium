using System;
using System.Collections.Generic;
using System.Text;

namespace Auditorium.Core.Entities
{
    internal class User
    {
        private int Id;
        private string Login;
        private string PasswordHash;
        public string GetDisplayName(string username)
        {
            return username;
        }
    }
}
