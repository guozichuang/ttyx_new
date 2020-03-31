using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using Simon.Common;
using System.Configuration;

public partial class Test : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string Admin_KeyID = GetAppSettings("api_keyid");
        string Admin_Secret = GetAppSettings("api_secret");
        Response.Write(SimonUtils.EnCodeMD5(Admin_KeyID + "1600000000" + Admin_Secret));
    }

    public static string GetAppSettings(string key)
    {
        if (ConfigurationManager.AppSettings[key] != null)
        {
            return ConfigurationManager.AppSettings[key];
        }
        return "";
    }
}