using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Utilities.Security.SecurityUtilities
{
    public interface IUserIdentity
    {
        string GetName();
    }

    public class StandardUserIndentity : IUserIdentity
    {
        public string GetName()
        {
            // 1. Access the environment's user property
            var osUserName = Environment.UserName;

            // 2. Return or use the variable
            return osUserName;
        }
    }
}
