using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace AutoSyncData
{
    public partial class Service1 : ServiceBase
    {
        MySqlConnection con = null;
        public static String DbPath = "", TimerCallSec = "";
        public static Boolean DebugLog = false, fetchVendorsData = true, fetchPOData = false, fetchVehicles = false, fetchInOutVehicles = false;
        public static String logsPath = "";
        public static String ErrorLogsPath = "";
        public string ServiceDir = AppDomain.CurrentDomain.BaseDirectory;
        private System.Timers.Timer timer;


        public Service1()
        {
            InitializeComponent();
            Boolean result = ReadFile();
        }

        protected override void OnStart(string[] args)
        {
            WriteToLogFile("Service is started at " + DateTime.Now);
            // this.timer = new System.Timers.Timer(30000D);  // 30000 milliseconds = 30 seconds
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

                while (((line = myfile.ReadLine()) != null) || (count < 3))
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
            /**************** Fecth Vendors Data *****************/
            if (fetchVendorsData == true)
            {
                if (DebugLog == true)
                {
                    WriteToLogFile("Finding Vendors Data: " + DateTime.Now);
                }
                checkVendorsData();
            }

            /**************** Fecth Vendors PO Data *****************/
            if (fetchPOData == true)
            {
                if (DebugLog == true)
                {
                    WriteToLogFile("Finding Vendors PO Data: " + DateTime.Now);
                }
                checkPOData();
            }

            /**************** Fecth Vendors Vehicles Data *****************/
            if (fetchVehicles == true)
            {
                if (DebugLog == true)
                {
                    WriteToLogFile("Finding Vendors Vehicles Data: " + DateTime.Now);
                }
                checkVehiclesData();
            }

            /**************** Fecth IN/OUT Vehicles Log Data *****************/
            if (fetchInOutVehicles == true)
            {
                if (DebugLog == true)
                {
                    WriteToLogFile("Finding IN/OUT Vehicles Data: " + DateTime.Now);
                }
                checkVehiclesInOutData();
            }
        }

        /*************************** Check Vendors Data *****************************/
        public void checkVendorsData()
        {
            try
            {
                string id = "";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT synctablesdata.id, synctablesdata.tablerowid, vendors.vendorname, vendors.license, vendors.state, vendors.city, vendors.street, vendors.pincode, vendors.gstno, synctablesdata.actiontype FROM vendors " +
                    "INNER JOIN synctablesdata ON synctablesdata.tablerowid = vendors.id " +
                    "WHERE synctablesdata.syncstate = 'Pending' AND tabletype = 'VENDORS' ORDER BY synctablesdata.id ASC";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    id = Reader["id"].ToString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        // MessageBox.Show(Reader["id"].ToString());
                        updateVendorsInExcel(id, Reader["tablerowid"].ToString(), Reader["vendorname"].ToString(), Reader["license"].ToString(), Reader["state"].ToString(), Reader["city"].ToString(), Reader["street"].ToString(), Reader["pincode"].ToString(), Reader["gstno"].ToString(), Reader["actiontype"].ToString());
                    }
                }

                if (string.IsNullOrEmpty(id))
                {
                    fetchVendorsData = false; fetchPOData = true; fetchVehicles = false; fetchInOutVehicles = false;
                }
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("checkVendorsData: " + ex.Message);
            }

        }

        public void updateVendorsInExcel(string rowId, string vendorid, string vendorname, string vendorlicense, string vendorstate, string vendorcity, string vendorstreet, string vendorpincode, string vendorgstno, string actiontype)
        {
            String todayDate = DateTime.Now.ToString("yyyy MM dd");

            string dir = ServiceDir + "\\Sync Data\\" + todayDate;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string path = dir + "\\vendorsdata.csv";

            // Set the variable "delimiter" to ", ".
            string delimiter = ", ";

            // This text is added only once to the file.
            if (!File.Exists(path))
            {
                // Create a file to write to.
                string createText = "Vendor ID" + delimiter + "Vendor Name" + delimiter + "Vendor License" + delimiter + "Vendor State" + delimiter + "Vendor City" + delimiter + "Vendor Street" + delimiter + "Vendor Pincode" + delimiter + "Vendor Gstno" + delimiter + "Action Type" + Environment.NewLine;
                File.WriteAllText(path, createText);
            }

            string appendText = vendorid + delimiter + vendorname + delimiter + vendorlicense + delimiter + vendorstate + delimiter + vendorcity + delimiter + vendorstreet + delimiter + vendorpincode + delimiter + vendorgstno + delimiter + actiontype + delimiter + Environment.NewLine;
            File.AppendAllText(path, appendText);

            // Update Vendor ID
            try
            {
                string Query = "UPDATE synctablesdata SET syncstate= 'Done' WHERE id= '" + rowId + "'";
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
                WriteToErrorFile("UPDATE synctablesdata: " + ex.Message);
            }

        }


        /*************************** Check Vendors PO Data *****************************/
        public void checkPOData()
        {
            try
            {
                string id = "";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT synctablesdata.id, synctablesdata.tablerowid, po.ponumber, po.postartdate, po.poenddate, po.podate, po.whichvendor, synctablesdata.actiontype FROM po " +
                                    "INNER JOIN synctablesdata ON synctablesdata.tablerowid = po.id " +
                                    "WHERE synctablesdata.syncstate = 'Pending' AND synctablesdata.tabletype = 'PO' ORDER BY synctablesdata.id ASC";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    id = Reader["id"].ToString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        // MessageBox.Show(Reader["id"].ToString());
                        updateVendorsPOInExcel(id, Reader["tablerowid"].ToString(), Reader["ponumber"].ToString(), Reader["postartdate"].ToString(), Reader["poenddate"].ToString(), Reader["whichvendor"].ToString(), Reader["actiontype"].ToString());
                    }
                }

                if (string.IsNullOrEmpty(id))
                {
                    fetchVendorsData = false; fetchPOData = false; fetchVehicles = true; fetchInOutVehicles = false;
                }
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("checkPOData: " + ex.Message);
            }

        }

        public void updateVendorsPOInExcel(string rowId, string poId, string ponumber, string postartdate, string poenddate, string whichvendor, string actiontype)
        {
            String todayDate = DateTime.Now.ToString("yyyy MM dd");

            string dir = ServiceDir + "\\Sync Data\\" + todayDate;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string path = dir + "\\podata.csv";

            // Set the variable "delimiter" to ", ".
            string delimiter = ", ";

            // This text is added only once to the file.
            if (!File.Exists(path))
            {
                // Create a file to write to.
                string createText = "Vendor ID" + delimiter + "PO ID" + delimiter + "PO Number" + delimiter + "PO StartDate" + delimiter + "PO EndDate" + delimiter + "Action Type" + Environment.NewLine;
                File.WriteAllText(path, createText);
            }

            string appendText = whichvendor + delimiter + poId + delimiter + ponumber + delimiter + postartdate + delimiter + poenddate + delimiter + actiontype + delimiter + Environment.NewLine;
            File.AppendAllText(path, appendText);

            // Update Vendor ID
            try
            {
                string Query = "UPDATE synctablesdata SET syncstate= 'Done' WHERE id= '" + rowId + "'";
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
                WriteToErrorFile("UPDATE synctablesdata: " + ex.Message);
            }

        }


        /*************************** Check Vendors Vehicles Data *****************************/
        public void checkVehiclesData()
        {
            try
            {
                string id = "";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT synctablesdata.id, synctablesdata.tablerowid, vehicles.whichVendor, vehicles.vehiclenumber, vehicles.insuranceexpirydate, vehicles.pollutionexpirydate, vehicles.teirtype, vehicles.transportername, vehicles.rfidtagid, synctablesdata.actiontype FROM vehicles " +
                                    "INNER JOIN synctablesdata ON synctablesdata.tablerowid = vehicles.id " +
                                    "WHERE synctablesdata.syncstate = 'Pending' AND synctablesdata.tabletype = 'VEHICLES' ORDER BY synctablesdata.id ASC";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    id = Reader["id"].ToString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        // MessageBox.Show(Reader["id"].ToString());
                        updateVendorsVehiclesInExcel(id, Reader["tablerowid"].ToString(), Reader["whichvendor"].ToString(), Reader["vehiclenumber"].ToString(), Reader["insuranceexpirydate"].ToString(), Reader["pollutionexpirydate"].ToString(), Reader["teirtype"].ToString(), Reader["transportername"].ToString(), Reader["rfidtagid"].ToString(), Reader["actiontype"].ToString());
                    }
                }

                if (string.IsNullOrEmpty(id))
                {
                    fetchVendorsData = false; fetchPOData = false; fetchVehicles = false; fetchInOutVehicles = true;
                }
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("checkVehiclesData: " + ex.Message);
            }

        }

        public void updateVendorsVehiclesInExcel(string rowId, string vehicleId, string whichvendor, string vehiclenumber, string insuranceexpirydate, string pollutionexpirydate, string teirtype, string transportername, string rfidtagid, string actiontype)
        {
            String todayDate = DateTime.Now.ToString("yyyy MM dd");

            string dir = ServiceDir + "\\Sync Data\\" + todayDate;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string path = dir + "\\vehiclesdata.csv";

            // Set the variable "delimiter" to ", ".
            string delimiter = ", ";

            // This text is added only once to the file.
            if (!File.Exists(path))
            {
                // Create a file to write to.
                string createText = "Vendor ID" + delimiter + "Vehicle ID" + delimiter + "Vehicle Number" + delimiter + "Insurance Expiry Date" + delimiter + "Pollution Expiry Date" + delimiter + "Teir Type" + delimiter + "Transporter Name" + delimiter + "RFID Tag Id" + delimiter + "Action Type" + Environment.NewLine;
                File.WriteAllText(path, createText);
            }

            string appendText = whichvendor + delimiter + vehicleId + delimiter + vehiclenumber + delimiter + insuranceexpirydate + delimiter + pollutionexpirydate + delimiter + teirtype + delimiter + transportername + delimiter + rfidtagid + delimiter + actiontype + delimiter + Environment.NewLine;
            File.AppendAllText(path, appendText);

            // Update Vendor ID
            try
            {
                string Query = "UPDATE synctablesdata SET syncstate= 'Done' WHERE id= '" + rowId + "'";
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
                WriteToErrorFile("UPDATE synctablesdata: " + ex.Message);
            }

        }


        /*************************** Check Vendors Vehicles Data *****************************/
        public void checkVehiclesInOutData()
        {
            try
            {
                string tripId = "";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT id, vehiclenumber, indatetime, entry_reason, teirweight, teirweightdatetime, tare_reason, grossweight, grossdatetime, gross_reason, materialweight, po_number, lineweight, materialtype, challannumber, challanweight, weightdifference, outdatetime, printdatetime, stageno FROM vehicleinoutlogs WHERE stagesync = 'Pending' ORDER BY id ASC";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    tripId = Reader["id"].ToString();
                    if (!string.IsNullOrEmpty(tripId))
                    {
                        // MessageBox.Show(Reader["id"].ToString());
                        updateInOutVehiclesInExcel(tripId, Reader["vehiclenumber"].ToString(), Reader["indatetime"].ToString(), Reader["entry_reason"].ToString(), Reader["teirweight"].ToString(), Reader["teirweightdatetime"].ToString(), Reader["tare_reason"].ToString(), Reader["grossweight"].ToString(), Reader["grossdatetime"].ToString(), Reader["gross_reason"].ToString(), Reader["materialweight"].ToString(), Reader["po_number"].ToString(), Reader["lineweight"].ToString(), Reader["materialtype"].ToString(), Reader["challannumber"].ToString(), Reader["challanweight"].ToString(), Reader["weightdifference"].ToString(), Reader["outdatetime"].ToString(), Reader["printdatetime"].ToString(), Reader["stageno"].ToString());
                    }
                }

                if (string.IsNullOrEmpty(tripId))
                {
                    fetchVendorsData = true; fetchPOData = false; fetchVehicles = false; fetchInOutVehicles = false;
                }
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("checkVehiclesInOutData: "+ex.Message);
            }

        }

        public void updateInOutVehiclesInExcel(string tripId, string vehiclenumber, string indatetime, string entry_reason, string tareweight, string tareweightdatetime, string tare_reason, string grossweight, string grossweightdatetime, string gross_reason, string materialweight, string po_number, string lineweight, string materialtype, string challannumber, string challanweight, string weightdifference, string outdatetime, string printdatetime, string stageno)
        {
            String todayDate = DateTime.Now.ToString("yyyy MM dd");

            string dir = ServiceDir + "\\Sync Data\\" + todayDate;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string path = dir + "\\inoutvehicleslogsdata.csv";

            // Set the variable "delimiter" to ", ".
            string delimiter = ", ";

            // This text is added only once to the file.
            if (!File.Exists(path))
            {
                // Create a file to write to.
                string createText = "Trip ID" + delimiter + "Vehicle Number" + delimiter +"In DateTime" + delimiter + "Entry Reason" + delimiter + "Tare Weight" + delimiter + "Tare Weight DateTime" + delimiter + "Tare Reason" + delimiter + "Gross Weight" + delimiter + "Gross Weight DateTime" + delimiter + "Gross Reason" + delimiter + "Material Weight" + delimiter + "PO Number" + delimiter + "Line" + delimiter + "Material Type" + delimiter + "Challan Number" + delimiter + "Challan Weight" + delimiter + "Weight Difference" + delimiter + "Out DateTime" + delimiter + "Print DateTime" + Environment.NewLine;
                File.WriteAllText(path, createText);
            }

            string appendText = tripId + delimiter + vehiclenumber + delimiter + indatetime + delimiter + entry_reason + delimiter + tareweight + delimiter + tareweightdatetime + delimiter + tare_reason + delimiter + grossweight + delimiter + grossweightdatetime + delimiter + gross_reason + delimiter + materialweight + delimiter + po_number + delimiter + lineweight + delimiter + materialtype + delimiter + challannumber + delimiter + challanweight + delimiter + weightdifference + delimiter + outdatetime + delimiter + printdatetime + delimiter + Environment.NewLine;
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


    }
}
