using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Routing;
using ItCraftTest.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ItCraftTest.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private IConfiguration Configuration;
        private readonly IOptions<AuthOptions> authOptions;

        string connString;
        SqlConnection connection;
        public UserController(IConfiguration _configuration, IOptions<AuthOptions> authOptions)
        {
            Configuration = _configuration;
            this.authOptions = authOptions;
            connString = this.Configuration.GetConnectionString("DefaultConnection");
            connection = new SqlConnection(connString);
        }


        [Route("index")]
        [HttpGet]
        public IActionResult Index()
        {            
            connection.Open();
            var result = ShowAll(connection);
            connection.Close();
            return Ok(result);
        }

        private string ShowAll(SqlConnection connection)
        {
            string command = "SELECT * FROM Users";
            SqlCommand sqlComm;
            SqlDataReader dataReader;
            string output = "";
            sqlComm = new SqlCommand(command, connection);
            dataReader = sqlComm.ExecuteReader();
            while (dataReader.Read())
                output += $"{dataReader.GetValue(0)}. Name: {dataReader.GetValue(1)};    Login: {dataReader.GetValue(2)};   Password: {dataReader.GetValue(3)};    Last Activity: {dataReader.GetValue(4)} \n";
            return output;
        }

        private void GenerateUsers(SqlConnection connection)
        {
            for (int temp = 1; temp <= 10; temp++)
            {
                string command = $"INSERT INTO Users (Name, Login, Password) VALUES ('Name{temp}', 'Login{temp}', 'Password{temp}')";
                SqlCommand sqlComm = new SqlCommand(command, connection);
                sqlComm.ExecuteNonQuery();
            }
        }

        private void ResetTable(SqlConnection connection)
        {
            string command = "DROP TABLE Users";
            SqlCommand sqlComm = new SqlCommand(command, connection);
            sqlComm.ExecuteNonQuery();
            command = "CREATE TABLE [dbo].[Users] (" +
                "[Id]                   INT           NOT NULL IDENTITY(1,1),\n" +
                "[Name]                 VARCHAR (255) NOT NULL,\n" +
                "[Login]                VARCHAR (255) NOT NULL,\n" +
                "[Password]             VARCHAR (255) NOT NULL,\n" +
                "[LastActivityTime]     DATETIME      NULL, \n" +
                "CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC)\n" +
            ");";
            sqlComm = new SqlCommand(command, connection);
            sqlComm.ExecuteNonQuery();
        }

        [Route("registration")]
        [HttpPost]
        public IActionResult Registration([FromForm] RegisterForm User)
        {
            var str = ValidateUser(User);
            if (str.Length > 0)
                return BadRequest(str);            

            connection.Open();

            string checkComm = $"SELECT * FROM Users WHERE Login = '{User.Login}'";
            SqlCommand sqlCheck = new SqlCommand(checkComm, connection);
            SqlDataReader dataReader = sqlCheck.ExecuteReader();
            if (dataReader.Read())
                return BadRequest("Login already exists");

            string command = $"INSERT INTO Users (Name, Login, Password, LastActivityTime) VALUES ('{User.Name}', '{User.Login}', '{User.Password}', '{DateTime.Now}')";
            SqlCommand sqlComm = new SqlCommand(command, connection);
            sqlComm.ExecuteNonQuery();
            connection.Close();

            User jwtUser = new User(User);
            var token = GenerateJWT(jwtUser);
            return Ok(new {
                access_token = token
            });
        }

        [Route("login")]
        [HttpPost]
        public IActionResult Login([FromForm] string login, [FromForm] string password)
        {
            string connString = this.Configuration.GetConnectionString("DefaultConnection");
            SqlConnection connection = new SqlConnection(connString);

            connection.Open();

            string checkComm = $"SELECT * FROM Users WHERE Login = '{login}'";
            SqlCommand sqlCheck = new SqlCommand(checkComm, connection);
            SqlDataReader dataReader = sqlCheck.ExecuteReader();

            if (dataReader.Read())
            {
                if (dataReader.GetValue(3).ToString() == password)
                {
                    string command = $"UPDATE Users SET LastActivityTime = '{DateTime.Now}' WHERE Login = '{login}'";
                    SqlCommand sql = new SqlCommand(command, connection);
                    sql.ExecuteNonQuery();

                    User jwtUser = new User();
                    jwtUser.Login = login;
                    jwtUser.Password = password;
                    var token = GenerateJWT(jwtUser);
                    return Ok(new
                    {
                        access_token = token
                    });
                }
            }

            return BadRequest("Something went wrong. Check your login or passport");
        }

        private string GenerateJWT(User User)
        {
            var authParams = authOptions.Value;

            var securityKey = authParams.GetSymmetricSecurityKey();
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.UniqueName, User.Login)
            };

            var token = new JwtSecurityToken(
                authParams.Issuer,
                authParams.Audience,
                claims,
                expires: DateTime.Now.AddSeconds(authParams.TokenLifeTime),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);

        }

        private string ValidateUser(RegisterForm User)
        {
            string result = "";
            if (User.Login == null || User.Name == null || User.Password == null || User.ConfirmPassword == null)
                return "fill in all fields";
            if (User.Name.Length > 255)
                result += "Name is too long \n";
            if (User.Login.Length > 255)
                result += "Login is too long \n";
            if (User.Password != User.ConfirmPassword)
                result += "Passwords don`t match \n";
            if (User.Password.Length > 255)
                result += "Password is too long";

            return result;
        }

        [Route("active-users")]
        [HttpGet]
        public IActionResult ShowActiveUsers()
        {
            string output = "";
            connection.Open();

            DateTime date = DateTime.Now.AddHours(-1);
            string command = $"SELECT Name, Login FROM Users WHERE LastActivityTime > '{date}'";
            SqlCommand sqlComm = new SqlCommand(command, connection);
            SqlDataReader dataReader = sqlComm.ExecuteReader();
            while (dataReader.Read())
            {
                output += $"Name: {dataReader.GetValue(0)};    Login: {dataReader.GetValue(1)} \n";
            }
            connection.Close();
            return Ok(output);
        }
    }
}
