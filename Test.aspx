<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="System.CodeDom.Compiler" %>
<%@ Import Namespace="System.Diagnostics" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<script runat="server">    
    void OnRun(object sender, EventArgs e)
    {
        const string dbName = "wcat"; // Word Catalog in MONGO
        const string clName = "words"; // Word Collection in MONGO
        
        try
        {
            System.IO.StreamReader fr = new System.IO.StreamReader(FileUpload.FileContent);
            string jsonContent = fr.ReadToEnd();

            if (Response.StatusCode == 200 && !string.IsNullOrEmpty(jsonContent))
            {
                Diagnostics.Text = string.Concat("Message is: ", jsonContent.Substring(0, 10)+"...");

                string localPath = Server.MapPath("~/Files");
                Diagnostics.Text = string.Concat("Path is: ", localPath);
                string dbPath = Server.MapPath("~/DataBase");
                Diagnostics.Text = string.Concat("DBPath is: ", dbPath);

                string fileName = System.IO.Path.Combine(localPath, "words.json");
                Diagnostics.Text = string.Concat("File is: ", fileName);
                System.IO.File.WriteAllText(fileName, jsonContent);

                string cmdExec = System.IO.Path.Combine(dbPath, "mongoimport.exe");
                string cmdLine = string.Format(" --db {0} --collection {1} --file {2} --drop",
                    dbName, clName, fileName);
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(cmdExec, cmdLine);
                    psi.CreateNoWindow = false;
                    psi.WorkingDirectory = System.IO.Path.GetFullPath(dbPath);
                    psi.ErrorDialog = false;
                    psi.RedirectStandardError = true;
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    using (Process p = new Process())
                    {
                        p.StartInfo = psi;
                        //p.ErrorDataReceived += new DataReceivedEventHandler(process_ErrorDataReceived);
                        //p.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
                        p.Start();

                        if (false == p.WaitForExit(60000))
                            throw new ArgumentException("The program '{0}' did not finish in time, aborting.", cmdExec);

                        if (p.ExitCode != 0)
                            throw new ArgumentException("Executables exit code is not 0, this is treated as an exception");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
            }
            else
            {
                Diagnostics.Text = string.Concat("Response is: ", Response.StatusDescription);
            }
        }
        catch (System.Net.WebException wex)
        {
            Diagnostics.Text = wex.Message;
        }
        catch (System.Exception ex)
        {
            Diagnostics.Text = ex.Message;
        }
    }
</script>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>TwitterAnalyzer Interface Import</title>
</head>
<body id="body1" bgcolor="#00aced">
    <form id="form1" runat="server">
        <div id="page"
            style="padding: 30px; position: fixed; left: 50px; width: 60%; overflow: visible;"
            align="left">
            <h1 style="padding: 8px; border: medium solid #999999; -moz-border-radius: 10px; -webkit-border-radius: 10px; width: auto; font-family: sans-serif; color: #333333;">TwitterAnalyzer Interface Import</h1>
            <div id="content"
                style="border: medium solid #999999; -moz-border-radius: 10px; -webkit-border-radius: 10px; padding: 30px; height: 325px; width: auto; font-family: sans-serif;">
                <p>
                    <asp:Label ID="LabelFileUpload" runat="server" Text="Choose JSON to import:"
                        Font-Names="sans-serif" ForeColor="#333333" Font-Size="Small"></asp:Label>
                    <br />
                    <asp:FileUpload ID="FileUpload" runat="server" Height="26"
                        Width="100%" Font-Names="sans-serif" Font-Size="Small" />
                    <br />
                    <asp:RequiredFieldValidator ID="fileValidator"
                        ControlToValidate="FileUpload"
                        Text="Please provide a JSON file"
                        runat="server" />
                </p>

                <p>
                    <asp:Button ID="ButtonRun" runat="server" Text="Run Import"
                        OnClick="OnRun" Height="38px" Width="78px" />
                </p>
                <hr />
                <p>
                    <asp:Label ID="Diagnostics" runat="server" Font-Bold="True"></asp:Label>
                </p>
            </div>
        </div>
    </form>
</body>
</html>
