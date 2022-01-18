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

namespace TareWeight
{
    public partial class Service1 : ServiceBase
    {
        MySqlConnection con = null;
        public static TcpClient client2;
        public static StreamReader STR;
        public static StreamWriter STW;
        static Boolean IsClient2_Connected = false;
        static Thread client_two;
        String cmd = "";
        String T2VehicleNum = "";
        public static String DbPath = "";
        public static String Server2_IP = "";
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
                client_two = new Thread(Client2);
                client_two.Start();
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

                while (((line = myfile.ReadLine()) != null) || (count < 3))
                {
                    if (count == 0)
                    {
                        DbPath = line;
                        DbPath = DbPath.Replace("DbPath=", "");
                    }
                    else if (count == 1)
                    {
                        Server2_IP = line;
                        Server2_IP = Server2_IP.Replace("Server2_IP=", "");
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
                WriteToErrorFile("Exception in ReadFile: " + DateTime.Now + "  " + ex.Message.ToString());
                return false;
            }
        }

        /*******************************Client 2************************************/
        public void Client2()
        {
            client2 = new TcpClient();
            IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(Server2_IP), 23);
            try
            {
                client2.ReceiveTimeout = 40000;

                client2.Connect(iPEnd);

                if (client2.Connected)
                {
                    STR = new StreamReader(client2.GetStream());
                    STW = new StreamWriter(client2.GetStream());
                    STW.AutoFlush = true;

                    STW.WriteLine("Hi Server");

                    IsClient2_Connected = true;
                    IsConnected = "connected";
                    Insert_Connectivity_Status("C2", "Connected"); // Function calling  (Connectivity Status)

                    if (DebugLog == true)
                    {
                        WriteToLogFile("client 2 Connected: " + DateTime.Now);
                    }

                    while (IsClient2_Connected)
                    {
                        cmd = "";
                        cmd = STR.ReadLine();

                        if (DebugLog == true)
                        {
                            WriteToLogFile("T2 cmd: " + cmd);
                        }
                        /************************************/

                        if (cmd.Length > 0)
                        {
                            if (cmd.StartsWith("|TAG-") && (cmd.EndsWith("%"))) //RFID tag command 
                            {
                                cmd = cmd.Remove(0, 5).Replace("%", "");

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
                                    String tareWeightis = cmd.Remove(0, 4).Replace("%", "");
                                    if (DebugLog == true)
                                    {
                                        WriteToLogFile("Weight is: " + tareWeightis + " and Vehicle Num: " + T2VehicleNum);
                                    }

                                    if (cmd.Length > 0)
                                    {
                                        Update_Logs_TareWeight(tareWeightis, T2VehicleNum);
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
                                    WriteToErrorFile("Weight not received: " + ex);
                                }
                            }
                            else if (cmd.StartsWith("|WBUSY-") && (cmd.EndsWith("%"))) // Vehicle Number command and weighing machine is busy
                            {
                                try
                                {
                                    String tare_busy_Weightis = cmd.Remove(0, 7).Replace("%", "");
                                    if (DebugLog == true)
                                    {
                                        WriteToLogFile("TARE - Weighing machine is busy, weight is: " + tare_busy_Weightis + " and Vehicle Num: " + T2VehicleNum);
                                    }

                                    if (cmd.Length > 0)
                                    {
                                        Insert_Logs_TareBusyWeighing(tare_busy_Weightis, T2VehicleNum);
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
                Thread.Sleep(20000);
                if (DebugLog == true)
                {
                    WriteToLogFile("Client2 retry is on: " + DateTime.Now);
                }

                if (IsConnected.Equals("connected"))
                {
                    IsConnected = "NotConncted";
                    Insert_Connectivity_Status("C2", "Disconnected");
                }
                
                IsClient2_Connected = false;

                if (client_two != null)
                    client_two = null;

                client_two = new Thread(Client2);
                client_two.Start();
            }
        }


        /****************************** INSERT CONNECTIVITY STATUS (Client 1) Function ******************* |OK% *******************/
        public void Insert_Connectivity_Status(String controlunit, String controlunitstatus)
        {
            var dateString = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
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


        /****************************** CHECK TAG VALID INVALID Function ******************** |INVALID% ********* |OPEN% *************/
        public void Check_Tag_Valid_Invalid(String TagId)
        {
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
                    STW.WriteLine("|OPEN%");
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();
                    T2VehicleNum = vehicleNumber;
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


        /****************************** UPDATE TARE WEIGHT Function ******************** |WT-OK% **********************/
        public void Update_Logs_TareWeight(string tareWeight, string vehiclenumber)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            String currentDate = dateString;
            try
            {
                string stageno = "2", stagesync = "Pending";
                string Query = "UPDATE vehicleinoutlogs SET teirweight= '" + tareWeight + "', teirweightdatetime= '" + currentDate + "', stageno= '" + stageno + "', stagesync= '" + stagesync + "' WHERE vehiclenumber= '" + vehiclenumber + "' ORDER BY id DESC";
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand MyCommand2 = new MySqlCommand(Query, con);
                MySqlDataReader MyReader2;
                con.Open();
                MyReader2 = MyCommand2.ExecuteReader();
                STW.WriteLine("|WT-OK%");
                WriteToLogFile("Tare weight: "+ tareWeight +" of vehicle num: "+ vehicleNumber+ " updated successfully");

                MyCommand2.Dispose();
                MyReader2.Dispose();
                con.Close();//Connection closed here  
            }
            catch (Exception ex)
            {
                con.Close();//Connection closed here  
                WriteToErrorFile("Tare Weight not updated: "+ ex.Message);
            }
        }


        /****************************** Insert TARE WEIGHING BUSY Function ******************** |WBUSY-OK% **********************/
        public void Insert_Logs_TareBusyWeighing(string tareWeightBusy, string vehiclenumber)
        {
            /* READ TRIP NO */
            string tripId = "";
            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT id FROM vehicleinoutlogs WHERE vehiclenumber= '" + vehiclenumber + "' ORDER BY id DESC LIMIT 1";
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
                                        "values('" + tripId + "','TARE','" + vehiclenumber + "','" + tareWeightBusy + "','" + msg + "')";
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
