using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GrossWeight
{
    public partial class Service1 : ServiceBase
    {
        MySqlConnection con = null;
        public static TcpClient client3;
        public static StreamReader STR;
        public static StreamWriter STW;
        static Boolean IsClient3_Connected = false;
        static Thread client_three;
        String cmd = "";
        String T3VehicleNum = "", tripID = "", inDateTime = "", tareWeight = "", printDatetime = "";
        public static String DbPath = "", Server3_IP = "", Retry_milisec = "";
        public static Boolean DebugLog = false;
        public static String logsPath = "";
        public static String ErrorLogsPath = "";
        public string ServiceDir = AppDomain.CurrentDomain.BaseDirectory;
        String IsConnected = "", vehicleNumber = "";

        public Service1()
        {
            InitializeComponent();
            Boolean result = ReadFile();

            if (result == true)
            {
                /**********************Client2 is Started....******************************/
                client_three = new Thread(Client3);
                client_three.Start();
            }
        }

        protected override void OnStart(string[] args)
        {
            WriteToLogFile("Service is started at " + DateTime.Now);
        }

        protected override void OnStop()
        {
            WriteToLogFile("Service is stopped at " + DateTime.Now);
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

                while (((line = myfile.ReadLine()) != null) || (count < 4))
                {
                    if (count == 0)
                    {
                        DbPath = line;
                        DbPath = DbPath.Replace("DbPath=", "");
                    }
                    else if (count == 1)
                    {
                        Server3_IP = line;
                        Server3_IP = Server3_IP.Replace("Server3_IP=", "");
                    }
                    else if (count == 2)
                    {
                        Retry_milisec = line;
                        Retry_milisec = Retry_milisec.Replace("Retry_milisec=", "");
                    }
                    else if (count == 3)
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
                WriteToErrorFile("Exception in ReadFile: " + DateTime.Now + "  " + ex.Message.ToString());
                return false;
            }
        }

        /*******************************Client 2************************************/
        public void Client3()
        {
            client3 = new TcpClient();
            IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(Server3_IP), 23);
            try
            {
                client3.ReceiveTimeout = int.Parse(Retry_milisec);

                client3.Connect(iPEnd);

                if (client3.Connected)
                {
                    STR = new StreamReader(client3.GetStream());
                    STW = new StreamWriter(client3.GetStream());
                    STW.AutoFlush = true;

                    STW.WriteLine("Hi Server");

                    IsClient3_Connected = true;
                    IsConnected = "connected";
                    Insert_Connectivity_Status("C3", "Connected"); // Function calling  (Connectivity Status)

                    if (DebugLog == true)
                    {
                        WriteToLogFile("client 3 Connected: " + DateTime.Now);
                    }

                    while (IsClient3_Connected)
                    {
                        cmd = "";
                        cmd = STR.ReadLine();

                        if (DebugLog == true)
                        {
                            WriteToLogFile("T3 cmd: " + cmd);
                        }
                        /************************************/

                        if (cmd.Length > 0)
                        {
                            if (cmd.StartsWith("|ID-") && (cmd.EndsWith("%"))) //RFID tag command 
                            {
                                cmd = cmd.Remove(0, 4).Replace("%", "");

                                if (cmd.Length > 0)
                                {
                                    Check_Tag_Valid_Invalid(cmd); // Function calling  (Check->  OPEN, INVALID)
                                }
                                else
                                {
                                    STW.WriteLine("|INVALID%");
                                }
                            }
                            else if (cmd.StartsWith("|WT-") && (cmd.EndsWith("%"))) // Vehicle Number command and weight 1 command
                            {
                                try
                                {
                                    String grossWeightis = cmd.Remove(0, 4).Replace("%", "");
                                    if (DebugLog == true)
                                    {
                                        WriteToLogFile("Weight is: " + grossWeightis + " and Vehicle Num: " + T3VehicleNum);
                                    }

                                    if (cmd.Length > 0)
                                    {
                                        Update_Logs_GrossWeight(grossWeightis, tripID, T3VehicleNum);
                                    }
                                    else
                                    {
                                        STW.WriteLine("|WT-OK%");

                                        if (DebugLog == true)
                                        {
                                            WriteToLogFile("Else |WT-OK%");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WriteToErrorFile("Gross Weight not received: " + ex);
                                }
                            }
                            else if (cmd.StartsWith("|WBUSY-") && (cmd.EndsWith("%"))) // Vehicle Number command and weighing machine is busy
                            {
                                try
                                {
                                    String tare_busy_Weightis = cmd.Remove(0, 7).Replace("%", "");
                                    if (DebugLog == true)
                                    {
                                        WriteToLogFile("TARE - Weighing machine is busy, weight is: " + tare_busy_Weightis + " and Vehicle Num: " + T3VehicleNum);
                                    }

                                    if (cmd.Length > 0)
                                    {
                                        Insert_Logs_TareBusyWeighing(tare_busy_Weightis, T3VehicleNum);
                                    }
                                    else
                                    {
                                        STW.WriteLine("|WBUSY-OK%");

                                        if (DebugLog == true)
                                        {
                                            WriteToLogFile("Else |WBUSY-OK%");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WriteToErrorFile("Weight not received: " + ex);
                                }
                            }
                            else if (cmd.StartsWith("|HLT") && (cmd.EndsWith("%")))
                            {
                                STW.WriteLine("|OK%");
                                if (DebugLog == true)
                                {
                                    WriteToLogFile("Health: |OK%");
                                }
                            }
                        }
                    }   // end of while loop
                }
            }
            catch (Exception ex)
            {
                Thread.Sleep(int.Parse(Retry_milisec));
                if (DebugLog == true)
                {
                    WriteToLogFile("Client3 retry is on: " + DateTime.Now);
                }

                if (IsConnected.Equals("connected"))
                {
                    IsConnected = "NotConncted";
                    Insert_Connectivity_Status("C3", "Disconnected");
                }

                IsClient3_Connected = false;

                if (client_three != null)
                    client_three = null;

                client_three = new Thread(Client3);
                client_three.Start();
            }
        }


        /****************************** INSERT CONNECTIVITY STATUS (Client 3) Function ******************* |OK% *******************/
        public void Insert_Connectivity_Status(String controlunit, String controlunitstatus)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            String DateTimeis = dateString;

            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                con.Open();
                MySqlCommand cmd = con.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "INSERT INTO connectivitystatus(controlunit, controlunitstatus, lastsync) values('" + controlunit + "','" + controlunitstatus + "','" + DateTimeis + "')";
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Insert_Connectivity_Status Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** CHECK TAG VALID INVALID Function ******************** |INVALID% ********* Validate Trip Entry & Tare *************/
        public void Check_Tag_Valid_Invalid(String TagId)
        {
            vehicleNumber = "";

            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT vehiclenumber FROM vehicles WHERE rfidtagid= '" + TagId + "'";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    vehicleNumber = Reader["vehiclenumber"].ToString();
                    if (DebugLog == true)
                    {
                        WriteToLogFile("vehicleNumber is:" + vehicleNumber);
                    }
                }
                if (string.IsNullOrEmpty(vehicleNumber))
                {
                    // Invalid Tag
                    STW.WriteLine("|INVALID%");

                    if (DebugLog == true)
                    {
                        WriteToLogFile("|INVALID%");
                    }
                }
                else
                {
                    // Valid Tag
                    // STW.WriteLine("|OPEN%");
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();
                    T3VehicleNum = vehicleNumber;
                    // Validate Trip Entry & Tare - (Trip new entry & tare exists or not)
                    Validate_Trip_Entry_Tare(vehicleNumber);
                }

                Reader.Close();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Check_Tag_Valid_Invalid_TareWeight Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** VALIDATE VEHICLE NEW ENTRY & TARE WEIGHT AVAILABLE OR NOT Function ******************** |OPEN% ********* |ENTRYPENDING% & |TAREPENDING% *************/
        public void Validate_Trip_Entry_Tare(String vehicleNumber)
        {
            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT id, indatetime, teirweight, printdatetime FROM vehicleinoutlogs WHERE vehiclenumber = '" + vehicleNumber + "' ORDER BY id DESC LIMIT 1";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    tripID = Reader["id"].ToString();
                    inDateTime = Reader["indatetime"].ToString();
                    tareWeight = Reader["teirweight"].ToString();
                    printDatetime = Reader["printdatetime"].ToString();
                }
                // Check - If tripid is not null AND indatetime is not null AND vehiclenumber is not null AND tareWeight is not null AND printDatetime is null
                if ((!string.IsNullOrEmpty(tripID)) && (!string.IsNullOrEmpty(inDateTime)) && (!string.IsNullOrEmpty(vehicleNumber)) && (!string.IsNullOrEmpty(tareWeight)) && (string.IsNullOrEmpty(printDatetime)))
                {
                    // Valid Entry
                    STW.WriteLine("|OPEN%");

                    if (DebugLog == true)
                    {
                        WriteToLogFile("|OPEN% - Tag is valid, Entry pass for gross weight vehicle:" + vehicleNumber);
                    }
                }
                else if(string.IsNullOrEmpty(inDateTime))
                {
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();

                    STW.WriteLine("|ENTRYPENDING%");

                    if (DebugLog == true)
                    {
                        WriteToLogFile("|ENTRYPENDING% - Tag is valid, but entry not found of vehicle: " + vehicleNumber);
                    }
                }
                else if (string.IsNullOrEmpty(tareWeight))
                {
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();

                    STW.WriteLine("|TAREPENDING%");

                    if (DebugLog == true)
                    {
                        WriteToLogFile("|TAREPENDING% - Tag is valid, but tare weight not found of vehicle: " + vehicleNumber);
                    }
                }

                Reader.Close();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Validate_Trip_Entry Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** UPDATE GROSS WEIGHT Function ******************** |WT-OK% **********************/
        public void Update_Logs_GrossWeight(string grossWeight, string tripID, string vehiclenumber)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            String currentDate = dateString;
            string tareWeight = "";
            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT teirweight from vehicleinoutlogs where id= '" + tripID + "' ORDER BY id DESC LIMIT 1";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    // MessageBox.Show(Reader.GetValue(0).ToString());
                    tareWeight = Reader["teirweight"].ToString();
                }
                Reader.Dispose();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Tare Weight not found: " + ex.Message);
            }

            try
            {
                if (string.IsNullOrEmpty(tareWeight) || (Int32.Parse(tareWeight) < 0))
                {
                    // Tare Weight not found!
                    STW.WriteLine("|TAREPENDING%");
                    WriteToLogFile("Tare Weight not found");
                    Insert_Voilation(vehiclenumber); // Insert voliation stage 3
                }
                else
                {
                    if (con == null)
                    {
                        con = new MySqlConnection(DbPath.ToString());
                    }
                    string stageno = "3", stagesync = "Pending";
                    int materialweight = (Int32.Parse(grossWeight) - Int32.Parse(tareWeight));
                    string Query = "UPDATE vehicleinoutlogs SET grossweight= '" + grossWeight + "', grossdatetime= '" + currentDate + "', materialweight = '" + materialweight + "', stageno= '" + stageno + "', stagesync= '" + stagesync + "' WHERE id= '" + tripID + "' ORDER BY id DESC";
                    con = new MySqlConnection(DbPath.ToString());
                    MySqlCommand MyCommand2 = new MySqlCommand(Query, con);
                    MySqlDataReader MyReader2;
                    con.Open();
                    MyReader2 = MyCommand2.ExecuteReader();
                    STW.WriteLine("|WT-OK%");
					if (DebugLog == true)
					{
						WriteToLogFile("Gross weight: " + grossWeight + " of vehicle num: " + vehiclenumber + " updated successfully");
					}
                    MyReader2.Dispose();
                    MyCommand2.Dispose();
                    con.Close();//Connection closed here  
                }
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Gross Weight not updated: " + ex.Message);
            }
        }

        /****************************** INSERT VOILATION STAGE Function **************************************/
        public void Insert_Voilation(String vehiclenumber)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            String DateTimeis = dateString, voilationStage = "3", voilationMessage = "Tare weight pending of this vehicle, but it was come on Gross weight.";

            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                con.Open();
                MySqlCommand cmd = con.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "INSERT INTO voilation(vehilcenumber, voilationstage, message, created_at) values('" + vehiclenumber + "','" + voilationStage + "','" + voilationMessage + "','" + DateTimeis + "')";
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Voilation Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** Insert GROSS WEIGHING BUSY Function ******************** |WBUSY-OK% **********************/
        public void Insert_Logs_TareBusyWeighing(string tareWeightBusy, string vehiclenumber)
        {
            /* READ TRIP NO */
            string tripId = "";
            try
            {
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT id FROM vehicleinoutlogs WHERE vehiclenumber= '" + vehiclenumber + "' ORDER BY id DESC LIMIT 1";
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    tripId = Reader["id"].ToString();
                }
                myComm.Dispose();
                Reader.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Read TripId: " + ex.Message);
            }

            /* IF TRIP NO IS NOT EMPTY, THEN INSERT */
            if (!string.IsNullOrEmpty(tripId))
            {
                try
                {
                    string msg = "Weigh bridge busy.";
                    if (con == null)
                    {
                        con = new MySqlConnection(DbPath.ToString());
                    }
                    con.Open();
                    MySqlCommand cmd = con.CreateCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "INSERT INTO wbusy(tripid, stage, vehiclenumber, weight, msg) " +
                                        "values('" + tripId + "','GROSS','" + vehiclenumber + "','" + tareWeightBusy + "','" + msg + "')";
                    cmd.ExecuteNonQuery();
                    STW.WriteLine("|WBUSY-OK%");
                    cmd.Dispose();
                    con.Close();
                }
                catch (Exception ex)
                {
                    con.Close();
                    WriteToErrorFile("Insert_Weighing_Busy Exception : " + DateTime.Now + "  " + ex.Message.ToString());
                }
            }
        }
    }
}
