using MySql.Data.MySqlClient;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace AutoSyncData
{
    public partial class Service1 : ServiceBase
    {
        MySqlConnection con = null;
        public static String DbPath = "", TimerCallSec = "", FtpIP = "", FtpUsername = "", FtpPassword = "", FilePath = "";
        public static Boolean DebugLog = false, fetchVendorsData = true, fetchPOData = false, fetchVehicles = false, fetchInOutVehicles = false;
        public static String logsPath = "";
        public static String ErrorLogsPath = "";
        public string ServiceDir = AppDomain.CurrentDomain.BaseDirectory;
        private System.Timers.Timer timer;
        string path = "";

        public Service1()
        {
            InitializeComponent();
            Boolean result = ReadFile();
        }

        protected override void OnStart(string[] args)
        {
            WriteToLogFile("Service is started at " + DateTime.Now);
            this.timer = new System.Timers.Timer(Int32.Parse(TimerCallSec) * 1000D);  // Dynamic Time Seconds
            this.timer.AutoReset = true;
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timer_Elapsed);
            this.timer.Start();
        }

        protected override void OnStop()
        {
            WriteToLogFile("Service is stopped at " + DateTime.Now);
            this.timer.Stop();
            this.timer = null;
        }

        /***************************WriteToLogFile*******************************/
        public void WriteToLogFile(string Message)
        {
            if (!File.Exists(ServiceDir + "\\ConnectivityLogs.txt"))
            {
                // Create a file to write to.   
                StreamWriter sw1 = File.CreateText(ServiceDir + "\\ConnectivityLogs.txt");

                sw1.WriteLine(Message);
                sw1.Close();
            }
            else
            {
                try
                {
                    StreamWriter sw1 = File.AppendText(ServiceDir + "\\ConnectivityLogs.txt");

                    sw1.WriteLine(Message);
                    sw1.Close();
                }
                catch (Exception ex)
                {
                    // Console.WriteLine(ex);
                }
            }
        }

        /***************************WriteToLogFile*******************************/
        public void WriteToErrorFile(string Message)
        {
            if (!File.Exists(ServiceDir + "\\ErrorLogs.txt"))
            {
                // Create a file to write to.   
                StreamWriter sw1 = File.CreateText(ServiceDir + "\\ErrorLogs.txt");

                sw1.WriteLine(Message);
                sw1.Close();
            }
            else
            {
                try
                {
                    StreamWriter sw1 = File.AppendText(ServiceDir + "\\ErrorLogs.txt");

                    sw1.WriteLine(Message);
                    sw1.Close();
                }
                catch (Exception ex)
                {
                    // Console.WriteLine(ex);
                }
            }
        }

        /***************************Read Text File*******************************/
        public Boolean ReadFile()
        {
            try
            {
                int count = 0;
                String line = null;
                String file = ServiceDir + "\\config.txt";

                logsPath = ServiceDir + "\\ConnectivityLogs.txt";
                ErrorLogsPath = ServiceDir + "\\ErrorLogs.txt";

                StreamReader myfile = new StreamReader(file);

                while (((line = myfile.ReadLine()) != null) || (count < 7))
                {
                    if (count == 0)
                    {
                        DbPath = line;
                        DbPath = DbPath.Replace("DbPath=", "");
                    }
                    if (count == 1)
                    {
                        TimerCallSec = line;
                        TimerCallSec = TimerCallSec.Replace("TimerCallSec=", "");
                    }
                    else if (count == 2)
                    {
                        String Log = line;
                        Log = Log.Replace("DebugLog=", "");

                        if (Log.Equals("True"))
                        {
                            DebugLog = true;
                        }
                        else
                        {
                            DebugLog = false;
                        }
                    }
                    if (count == 3)
                    {
                        FtpIP = line;
                        FtpIP = FtpIP.Replace("ftpip=", "");
                    }
                    if (count == 4)
                    {
                        FtpUsername = line;
                        FtpUsername = FtpUsername.Replace("ftpusername=", "");
                    }
                    if (count == 5)
                    {
                        FtpPassword = line;
                        FtpPassword = FtpPassword.Replace("ftppassword=", "");
                    }
                    if (count == 6)
                    {
                        FilePath = line;
                        FilePath = FilePath.Replace("filepath=", "");
                    }
                    count++;
                }

                myfile.Close();
                return true;
            }
            catch (Exception ex)
            {
                if (DebugLog == true)
                {
                    WriteToErrorFile("Exception in ReadFile: " + DateTime.Now + "  " + ex.Message.ToString());
                }
                return false;
            }
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            /**************** Fecth IN/OUT Vehicles Log Data *****************/
            if (DebugLog == true)
            {
              WriteToLogFile("Finding IN/OUT Vehicles Data: " + DateTime.Now);
            }

            checkVehiclesInOutData(); 
        }

        /*************************** Check Vehicles IN OUT Data *****************************/
        public void checkVehiclesInOutData()
        {
            try
            {
                string dir = ServiceDir + "\\SyncData\\";
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                path = dir + "\\inoutvehicleslogsdata.csv";

                // Remove .csv File
                string[] filePaths = Directory.GetFiles(dir, "*.csv");
                foreach (string filePath in filePaths)
                {
                    File.Delete(filePath);
                    if (DebugLog == true)
                    {
                        WriteToLogFile("File Updating: " + DateTime.Now);
                    }
                }


                string doNumber = "", tripId = "", challandate = "", inDate = "", inTime = "", tareWeightDate = "", tareWeightTime = "", grossWeightDate = "", grossWeightTime = "", outDate = "", outTime = "";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT vehicleinoutlogs.id, vehicleinoutlogs.vehiclenumber, vehicleinoutlogs.indatetime, vehicleinoutlogs.entry_reason, vehicleinoutlogs.teirweight, vehicleinoutlogs.teirweightdatetime, vehicleinoutlogs.tare_reason, vehicleinoutlogs.grossweight, vehicleinoutlogs.grossdatetime, vehicleinoutlogs.gross_reason, vehicleinoutlogs.materialweight, vehicleinoutlogs.outdatetime, vehicleinoutlogs.stageno, vehicleinoutlogs.po_number, vehicleinoutlogs.po_unit, vehicleinoutlogs.lineweight, vehicleinoutlogs.materialtype, vehicleinoutlogs.challannumber, vehicleinoutlogs.challanweight, vehicleinoutlogs.challandate, vehicleinoutlogs.weightdifference, vehicleinoutlogs.source, vehicleinoutlogs.destination, vehicleinoutlogs.pitpassnumber, vehicleinoutlogs.pitpassdate, vehicleinoutlogs.pitpassweight, vehicleinoutlogs.printdatetime, vehicleinoutlogs.remarks, vendors.vendorcode, vendors.vendorname, vendors.transportername FROM vehicles " +
                    "INNER JOIN vendors ON vendors.id = vehicles.whichVendor INNER JOIN vehicleinoutlogs ON vehicleinoutlogs.vehiclenumber = vehicles.vehiclenumber " +
                    "WHERE vehicleinoutlogs.stageno = '4' AND vehicleinoutlogs.created_at > now() - INTERVAL 15 day";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    tripId = Reader["id"].ToString();

                    if (!string.IsNullOrEmpty(Reader["indatetime"].ToString()))
                    {
                        string[] inDateTime = Reader["indatetime"].ToString().Split(' ');
                        DateTime indate_dt = DateTime.ParseExact(inDateTime[0].ToString(), "yyyy-mm-dd", CultureInfo.InvariantCulture);
                        inDate = indate_dt.ToString("dd-mm-yyyy", CultureInfo.InvariantCulture);
                        inTime = inDateTime[1];
                    }
                    if (!string.IsNullOrEmpty(Reader["teirweightdatetime"].ToString()))
                    {
                        string[] teirWeightDateTime = Reader["teirweightdatetime"].ToString().Split(' ');
                        DateTime taredate_dt = DateTime.ParseExact(teirWeightDateTime[0].ToString(), "yyyy-mm-dd", CultureInfo.InvariantCulture);
                        tareWeightDate = taredate_dt.ToString("dd-mm-yyyy", CultureInfo.InvariantCulture);
                        tareWeightTime = teirWeightDateTime[1];
                    }
                    if (!string.IsNullOrEmpty(Reader["grossdatetime"].ToString()))
                    {
                        string[] grossDateTime = Reader["grossdatetime"].ToString().Split(' ');
                        DateTime grossdate_dt = DateTime.ParseExact(grossDateTime[0].ToString(), "yyyy-mm-dd", CultureInfo.InvariantCulture);
                        grossWeightDate = grossdate_dt.ToString("dd-mm-yyyy", CultureInfo.InvariantCulture);
                        grossWeightTime = grossDateTime[1];
                    }
                    if (!string.IsNullOrEmpty(Reader["outdatetime"].ToString()))
                    {
                        string[] outDateTime = Reader["outdatetime"].ToString().Split(' ');
                        DateTime outdate_dt = DateTime.ParseExact(outDateTime[0].ToString(), "yyyy-mm-dd", CultureInfo.InvariantCulture);
                        outDate = outdate_dt.ToString("dd-mm-yyyy", CultureInfo.InvariantCulture);
                        outTime = outDateTime[1];
                    }
                    if (!string.IsNullOrEmpty(Reader["challandate"].ToString()))
                    {
                        DateTime challandate_dt = DateTime.ParseExact(Reader["challandate"].ToString(), "yyyy-mm-dd", CultureInfo.InvariantCulture);
                        challandate = challandate_dt.ToString("dd-mm-yyyy", CultureInfo.InvariantCulture);
                    }

                    if (!string.IsNullOrEmpty(tripId))
                    {
                        updateInOutVehiclesInExcel(tripId, Reader["vehiclenumber"].ToString(), Reader["materialtype"].ToString(), Reader["vendorcode"].ToString(), Reader["vendorname"].ToString(), Reader["transportername"].ToString(), Reader["challannumber"].ToString(), challandate, inDate, inTime, tareWeightDate, tareWeightTime, grossWeightDate, grossWeightTime, Reader["po_unit"].ToString(), Reader["po_number"].ToString(), Reader["lineweight"].ToString(), doNumber, Reader["pitpassnumber"].ToString(), Reader["source"].ToString(), Reader["destination"].ToString(), outDate, outTime, Reader["grossweight"].ToString(), Reader["teirweight"].ToString(), Reader["materialweight"].ToString(), Reader["challanweight"].ToString(), Reader["weightdifference"].ToString(), Reader["remarks"].ToString());
                    }
                }
                con.Close();

                // Upload File
                UploadFileToFTP();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("checkVehiclesInOutData: " + ex.Message);
            }

        }

        public void updateInOutVehiclesInExcel(string tripId, string vehiclenumber, string materialtype, string vendorcode, string vendorname, string transportername, string challannumber, string challandate, string inDate, string inTime, string tareWeightDate, string tareWeightTime, string grossWeightDate, string grossWeightTime, string po_unit, string po_number, string lineweight, string doNumber, string pitpassnumber, string source, string destination, string outDate, string outTime, string grossweight, string teirweight, string materialweight, string challanweight, string weightdifference, string remarks)
        {
            // Set the variable "delimiter" to ", ".
            string delimiter = ", ";

            // This text is added only once to the file. 
            if (!File.Exists(path))
            {
                // Create a file to write to.
                string createText = "Gatepass No" + delimiter + "Vehicle No" + delimiter + "Product" + delimiter + "Vendor Code" + delimiter + "Vendor Name" + delimiter + "Transpoter" + delimiter + "Challan No" + delimiter + "Challan Date" + delimiter + "Gate Entry Date" + delimiter + "Gate Entry time" + delimiter + "Tare weighment Date" + delimiter + "Tare weighment time" + delimiter + "Gross weighment Date" + delimiter + "Gross weighment time" + delimiter + "Plant Code" + delimiter + "PO No" + delimiter + "PO line item" + delimiter + "DO No" + delimiter + "Pitpass No" + delimiter + "Source" + delimiter + "Destination" + delimiter + "Gate Exit Date" + delimiter + "Gate Exit time" + delimiter + "Gross Wt" + delimiter + "Tare Wt" + delimiter + "Net Wt" + delimiter + "Challan Qty" + delimiter + "Wt Difference" + delimiter + "Remark" + Environment.NewLine;
                File.WriteAllText(path, createText);
            }

            string appendText = tripId + delimiter + vehiclenumber + delimiter + materialtype + delimiter + vendorcode + delimiter + vendorname + delimiter + transportername + delimiter + challannumber + delimiter + challandate + delimiter + inDate + delimiter + inTime + delimiter + tareWeightDate + delimiter + tareWeightTime + delimiter + grossWeightDate + delimiter + grossWeightDate + delimiter + po_unit + delimiter + po_number + delimiter + lineweight + delimiter + doNumber + delimiter + pitpassnumber + delimiter + source + delimiter + destination + delimiter + outDate + delimiter + outTime + delimiter + grossweight + delimiter + teirweight + delimiter + materialweight + delimiter + challanweight + delimiter + weightdifference + delimiter + remarks + Environment.NewLine;

            File.AppendAllText(path, appendText);

            // Update Vendor ID
            try
            {
                string Query = "UPDATE vehicleinoutlogs SET stagesync= 'Done' WHERE id= '" + tripId + "'";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand MyCommand2 = new MySqlCommand(Query, con);
                MySqlDataReader MyReader2;
                con.Open();
                MyReader2 = MyCommand2.ExecuteReader();
                con.Close();//Connection closed here  
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("UPDATE vehicleinoutlogs: " + ex.Message);
            }

        }

        /*************************** Upload File (.csv) *****************************/
        private void UploadFileToFTP()
        {
            try
            {
                /* WriteToLogFile("FTP URL: "+ FtpIP);
                WriteToLogFile("Ftp Username: " + FtpUsername);
                WriteToLogFile("Ftp Password: " + FtpPassword);
                WriteToLogFile("File Path: " + FilePath); */

                WebClient client = new WebClient();
                client.Credentials = new NetworkCredential(FtpUsername, FtpPassword); // FTP Username and FTP Password
                client.UploadFile(FtpIP + @"/inoutvehicleslogsdata.csv", @"" + FilePath); // FTP URL and FTP File Path

                if (DebugLog == true)
                {
                    WriteToLogFile("File Uploaded: " + DateTime.Now);
                }

            }
            catch (Exception ex)
            {
                WriteToErrorFile("Error is: " + ex);
                throw ex;
            }
        }

    }
}
