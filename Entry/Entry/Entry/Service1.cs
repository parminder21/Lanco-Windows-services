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

namespace Entry
{
    public partial class Service1 : ServiceBase
    {
        MySqlConnection con = null;// new MySqlConnection(DbPath);
        public static String DbPath = "";
        public static String Server1_IP = "";
        public string ServiceDir = AppDomain.CurrentDomain.BaseDirectory;
        public static String logsPath = "";
        public static String ErrorLogsPath = "";
        public static TcpClient client;
        public static StreamReader STR;
        public static StreamWriter STW;
        public static Boolean DebugLog = false;
        static Boolean IsClient1_Connected = false;
        static Thread client_one;
        String cmd = "";
        String IsConnected = "";


        public Service1()
        {
            InitializeComponent();

            Boolean result = ReadFile();

            if (result == true)
            {
                /**********************Client1 is Started....******************************/
                client_one = new Thread(Client1);
                client_one.Start();
            }
        }

        protected override void OnStart(string[] args)
        {
            WriteToLogFile("Service is started at " + DateTime.Now);
        }

        protected override void OnStop()
        {
            client_one.Abort();
            WriteToLogFile("Service is stopped at " + DateTime.Now);
        }

        /***************************WriteToLogFile*******************************/
        public void WriteToLogFile(string Message)
        {
            if (!File.Exists(logsPath))
            {
                StreamWriter sw1 = File.CreateText(logsPath);
                sw1.WriteLine(Message);
                sw1.Close();
            }
            else
            {
                try
                {
                    StreamWriter sw1 = File.AppendText(logsPath);
                    sw1.WriteLine(Message);
                    sw1.Close();
                }
                catch (Exception ex)
                {

                }
            }
        }

        /***************************WriteToErrorFile*******************************/
        public void WriteToErrorFile(string Message)
        {
            if (!File.Exists(ErrorLogsPath))
            {
                StreamWriter sw2 = File.CreateText(ErrorLogsPath);
                sw2.WriteLine(Message);
                sw2.Close();
            }
            else
            {
                StreamWriter sw2 = File.AppendText(ErrorLogsPath);
                sw2.WriteLine(Message);
                sw2.Close();
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
                        Server1_IP = line;
                        Server1_IP = Server1_IP.Replace("Server1_IP=", "");
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

        /*******************************Client 1************************************/
        public void Client1()
        {
            //  Thread.Sleep(20000);
            client = new TcpClient();
            // IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(Server1_IP), 80);
            IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(Server1_IP), 23);
            try
            {
                client.ReceiveTimeout = 40000;

                client.Connect(iPEnd);

                if (client.Connected)
                {
                    STR = new StreamReader(client.GetStream());
                    STW = new StreamWriter(client.GetStream());

                    STW.AutoFlush = true;

                    STW.WriteLine("Hi Server");

                    IsClient1_Connected = true;
                    IsConnected = "connected";
                    Insert_Connectivity_Status("C1", "Connected"); // Function calling  (Connectivity Status)

                    if (DebugLog == true)
                    {
                        WriteToLogFile("client 1 Connected: " + DateTime.Now);
                    }
                    
                    while (IsClient1_Connected)
                    {
                        cmd = "";
                        cmd = STR.ReadLine();
                        if (DebugLog == true)
                        {
                            WriteToLogFile("Entry : " + cmd);
                        }
                        /************************************/

                        if (cmd.Length > 0)
                        {
                            if (cmd.StartsWith("|HLT") && (cmd.EndsWith("%")))  // Health command
                            {
                                cmd = cmd.Remove(0, 4).Replace("%", "");
                                STW.WriteLine("|OK%");
                                if (DebugLog == true)
                                {
                                    WriteToLogFile("Health: |OK%");
                                }
                            }
                            else if (cmd.StartsWith("|TAG-") && (cmd.EndsWith("%"))) //RFID tag command 
                            {
                                cmd = cmd.Remove(0, 5).Replace("%", "");

                                if (DebugLog == true)
                                {
                                    WriteToLogFile("Tag is:" + cmd);
                                }

                                Check_Tag_Valid_Invalid_TareWeight(cmd); // Function calling  (Check->  OPEN, BLINK, INVALID)
                            }
                        }
                    }  // end of while loop
                }
            }
            catch (Exception ex)
            {
                Thread.Sleep(20000);
                if (DebugLog == true)
                {
                    WriteToLogFile("Client1 retry is on: " + DateTime.Now);
                }
                Insert_Connectivity_Status("C1", "Disconnected");


                if (IsConnected.Equals("connected"))
                {
                    IsConnected = "NotConncted";
                    Insert_Connectivity_Status("C2", "Disconnected");
                }

                IsClient1_Connected = false;
                if (client_one != null)
                    client_one = null;

                client_one = new Thread(Client1);
                client_one.Start();
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

        /****************************** CHECK TAG VALID INVALID Function ******************** |INVALID% **********************/
        public void Check_Tag_Valid_Invalid_TareWeight(String TagId)
        {
            try
            {
                String vehicleNumber = "";
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                    con.Open();
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
                    Send_Audio_Message("TAG "+ TagId, "Unauthorised Vehicle");
                }
                else
                {
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();
                    // check Tare Weight
                    Check_TareWeight(vehicleNumber);
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

        /****************************** CHECK THIS TAG TARE WEIGHT Function ************** |OPEN% *********** |BLINK% *****************/
        public void Check_TareWeight(String vehicleNumber)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd");
            String currentDate = dateString;

            try
            {
                String tareWeight = "", tareWeightDateTime = "";

                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT teirweight, teirweightdatetime from vehicleinoutlogs where vehiclenumber= '" + vehicleNumber + "' AND DATE(teirweightdatetime) = DATE('" + currentDate + "') AND teirweight > 0 ORDER BY id DESC LIMIT 1";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    tareWeight = Reader["teirweight"].ToString();
                    tareWeightDateTime = Reader["teirweightdatetime"].ToString();
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Tareweight is:" + tareWeight + ", Tareweightdatetime is:" + tareWeightDateTime);
                    }
                }
                if (string.IsNullOrEmpty(tareWeight) || (Int32.Parse(tareWeight) < 0) || string.IsNullOrEmpty(tareWeightDateTime))
                {
                    // Tare Weight Not Found
                    STW.WriteLine("|BLINK%");
                    if (DebugLog == true)
                    {
                        WriteToLogFile("|BLINK%");
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    Insert_Vehicle_Logs(vehicleNumber, "", "", "1", "Pending");
                    Send_Audio_Message(vehicleNumber, "Authorise Vehicle. Go to tare weight.");
                }
                else
                {
                    // Tare Weight Found
                    STW.WriteLine("|OPEN%");
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Tare Weight found");
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    Insert_Vehicle_Logs(vehicleNumber, tareWeight, tareWeightDateTime, "1", "Pending");
                    Send_Audio_Message(vehicleNumber, "Authorise Vehicle");
                }
                Reader.Dispose();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Check_TareWeight Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** INSERT VEHICLE LOGS (Client 1) Function ************************************/
        public void Insert_Vehicle_Logs(String vehicleNumber, String tareWeight, String teirweightdatetime, String stageno, String stagesync)
        {
            var dateString = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            String DateTimeis = dateString;
            String grossweight = "", grossdatetime = "", materialweight = "", outdatetime = "", po_number = "", lineweight = "", materialtype = "", challannumber = "", printdatetime = "";
            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                    
                }
                con.Open();
                MySqlCommand cmd = con.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "INSERT INTO vehicleinoutlogs(vehiclenumber, indatetime, teirweight, teirweightdatetime, grossweight, grossdatetime, materialweight, outdatetime, po_number, lineweight, materialtype, challannumber, printdatetime, stageno, stagesync) " +
                                                                "values('"+ vehicleNumber + "', '"+ DateTimeis + "', '" + tareWeight + "', '" + teirweightdatetime + "', '" + grossweight + "', '" + grossdatetime + "', '" + materialweight + "', '" + outdatetime + "', '" + po_number + "', '" + lineweight + "', '" + materialtype + "', '" + challannumber + "', '" + printdatetime + "', '" + stageno + "', '" + stagesync + "')";
                WriteToLogFile(vehicleNumber + "  Log Inserted");

                cmd.ExecuteNonQuery();
                cmd.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Insert_Vehicle_Logs Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** SEND AUDIO MESSAGE (INSERT/UPDATE) Function ******************************************/
        public void Send_Audio_Message(String vehilceNumber, String Message)
        {
            try
            {
                String audiomsgID = "", actionType = "IN", actionPlay = "New";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT id FROM audiomsg";
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                    
                }
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    audiomsgID = Reader["id"].ToString();
                }
                if (string.IsNullOrEmpty(audiomsgID))
                {
                    // Insert Audio Message
                    insertAudioMessage(actionType, vehilceNumber, Message, actionPlay);
                }
                else
                {
                    // Update Audio Message
                    updateAudioMessage(actionType, vehilceNumber, Message, actionPlay);
                }

                Reader.Close();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Send_Audio_Message Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** INSERT AUDIO MESSAGE Function **************************************/
        public void insertAudioMessage(String actionType, String vehilceNumber, String Message, String actionPlay)
        {
            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                    con.Open();
                }
                MySqlCommand cmd = con.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "INSERT INTO audiomsg(actiontype, vehiclenumber, msg, actionplay) values('" + actionType + "','" + vehilceNumber + "','" + Message + "','" + actionPlay + "')";
                cmd.ExecuteNonQuery();
                if (DebugLog == true)
                {
                    WriteToLogFile("Audio Message Inserted: " + Message);
                }
                cmd.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Insert_Audio_Message Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** UPDATE AUDIO MESSAGE Function **************************************/
        public void updateAudioMessage(String actionType, String vehilceNumber, String Message, String actionPlay)
        {
            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                string Query = "UPDATE audiomsg SET actiontype= '" + actionType + "', vehiclenumber= '" + vehilceNumber + "', msg = '" + Message + "', actionplay= '" + actionPlay + "' WHERE id= '1'";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand MyCommand2 = new MySqlCommand(Query, con);
                MySqlDataReader MyReader2;
                con.Open();
                MyReader2 = MyCommand2.ExecuteReader();
                if (DebugLog == true)
                {
                    WriteToLogFile("Audio Message Updated: " + Message);
                }
                con.Close(); //Connection closed here  
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Update_Audio_Message Exception: " + ex.Message);
            }
        }

    }
}
