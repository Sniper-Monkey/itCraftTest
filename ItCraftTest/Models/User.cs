﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ItCraftTest.Models
{
    public class User
    {
        public string Name { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public User (RegisterForm user)
        {
            Name = user.Name;
            Login = user.Login;
            Password = user.Password;
        }
        public User()
        {

        }
    }
}
