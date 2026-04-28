using System;
using System.Diagnostics;
using System.IO;

namespace Launcher.Functions;
/*Xephor, Origins, 76.234.105.233, 10300, 1, 5
 Spoofed Args for quick Login
    [0] 107.23.173.143
    [1] 10622
    [2] 57 Im not sure what this value is for (Possibly Server Number)
    [3] Account
    [4] Password
    [5] Character
    [6] Realm
Spoofed Args for standard login
    [0] 107.23.173.143
    [1] 10622
    [2] 50 Im not sure what this value is for (Possibly Server Number)
    [3] Account
    [4] Password
*/
public class Login
{
    /// <summary>
    /// Starts the game with simple authentication params
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public bool Start(string? username, string? password)
    {
        string gpath = string.Empty;
        bool quickin = true;
        string resolvedUsername = !Null(username)
            ? username
            : Settings.launcher.Default.UserName;

        string resolvedPassword = !Null(password)
            ? password
            : Settings.launcher.Default.SaveSettings
                ? Settings.launcher.Default.Password
                : null!;

        if (Null(resolvedUsername))
            throw new NullReferenceException("Account name is null or empty after loading settings.");

        if (Null(resolvedPassword))
            throw new NullReferenceException("Password is null or empty after loading settings.");

        string workingDirectory = Settings.launcher.Default.Directory;
        if (Null(workingDirectory) || !Directory.Exists(workingDirectory))
            workingDirectory = AppContext.BaseDirectory;

        string fileName = Settings.launcher.Default.FileName;
        if (Null(fileName))
            throw new NullReferenceException("Launch file name is null or empty.");

        //Use Connect.exe or not
        bool UseConnect = Settings.launcher.Default.UseConnect;
        if (UseConnect)
        {
            gpath = Path.IsPathRooted("connect.exe") ? fileName : Path.Combine(workingDirectory, "connect.exe");
        }
        else
        {
            gpath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(workingDirectory, fileName);
        }

        if (!File.Exists(gpath))
            throw new FileNotFoundException("Launch target was not found.", gpath);

        string ip = Settings.launcher.Default.RemoteIP;
        string port = Settings.launcher.Default.RemotePort.ToString();
        string param = Settings.launcher.Default.Profile.ToString();
        string ServerName = "Origins";
        string PlayerName = "Xephor";
        string PlayerRealm = "1";


        using Process myProcess = new();
        myProcess.StartInfo.WorkingDirectory = workingDirectory;
        myProcess.StartInfo.FileName = gpath;
        if (UseConnect)
        {
            if (quickin)
            {
                myProcess.StartInfo.Arguments = $"{fileName} {ip}:{port} {resolvedUsername} {resolvedPassword} {PlayerName} {PlayerRealm}";
            }
            else
            {
                myProcess.StartInfo.Arguments = $"{fileName} {ip}:{port} {resolvedUsername} {resolvedPassword}";
            }
        } 
        else
        {
            myProcess.StartInfo.Arguments = $"{ServerName} {ip} {port} {resolvedUsername} {resolvedPassword}";
        }

        //myProcess.StartInfo.Arguments = $"{ip},{port},{param},{resolvedUsername},{resolvedPassword}";
        //myProcess.StartInfo.Arguments = $"{ServerName}, {ip}, {port}, {resolvedUsername}, {resolvedPassword}";
        myProcess.StartInfo.UseShellExecute = UseConnect;

        try
        {
            return myProcess.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }
    /// <summary>
    /// Starts the game with simple authentication params
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public bool Start(string? username, string? password, string PlayerName, string PlayerRealm)
    {
        string gpath = string.Empty;

        //Quick Login used but contract to connect not supplied check
        if (Null(PlayerName) || Null(PlayerRealm)) return Start(username, password);

        string resolvedUsername = !Null(username)
            ? username
            : Settings.launcher.Default.UserName;

        string resolvedPassword = !Null(password)
            ? password
            : Settings.launcher.Default.SaveSettings
                ? Settings.launcher.Default.Password
                : null!;

        if (Null(resolvedUsername))
            throw new NullReferenceException("Account name is null or empty after loading settings.");

        if (Null(resolvedPassword))
            throw new NullReferenceException("Password is null or empty after loading settings.");

        string workingDirectory = Settings.launcher.Default.Directory;
        if (Null(workingDirectory) || !Directory.Exists(workingDirectory))
            workingDirectory = AppContext.BaseDirectory;

        string fileName = Settings.launcher.Default.FileName;
        if (Null(fileName))
            throw new NullReferenceException("Launch file name is null or empty.");

        //Use Connect.exe or not
        bool UseConnect = Settings.launcher.Default.UseConnect;
        if (UseConnect)
        {
            gpath = Path.IsPathRooted("connect.exe") ? fileName : Path.Combine(workingDirectory, "connect.exe");
        }
        else
        {
            gpath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(workingDirectory, fileName);
        }

        if (!File.Exists(gpath))
            throw new FileNotFoundException("Launch target was not found.", gpath);

        string ip = Settings.launcher.Default.RemoteIP;
        string port = Settings.launcher.Default.RemotePort.ToString();
        string param = Settings.launcher.Default.Profile.ToString();
        string ServerName = "Origins";


        using Process myProcess = new();
        myProcess.StartInfo.WorkingDirectory = workingDirectory;
        myProcess.StartInfo.FileName = gpath;
        if (UseConnect)
        {
            myProcess.StartInfo.Arguments = $"{fileName} {ip}:{port} {resolvedUsername} {resolvedPassword} {PlayerName} {PlayerRealm}";
        }
        else
        {
            #warning Connecting without using connect.exe is not yet supported
            myProcess.StartInfo.Arguments = $"{ServerName} {ip} {port} {resolvedUsername} {resolvedPassword}";
        }

        myProcess.StartInfo.UseShellExecute = UseConnect;

        try
        {
            return myProcess.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }
}