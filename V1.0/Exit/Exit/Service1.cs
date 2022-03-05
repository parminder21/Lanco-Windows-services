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

namespace Exit
{
    public partial class Service1 : ServiceBase
    {
        MySqlConnection con = null;
        public static TcpClient client4;
        public static StreamReader STR;
        public static StreamWriter STW;
        static Boolean IsClient4_Connected = false;
        static Thread client_four;
        String cmd = "";
        public static String DbPath = "";
        public static String Server4_IP = "", Retry_milisec = "";
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
                client_four = new Thread(Client4);
                client_four.Start();
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
                        Server4_IP = line;
                        Server4_IP = Server4_IP.Replace("Server4_IP=", "");
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
        public void Client4()
        {
            client4 = new TcpClient();
            IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(Server4_IP), 23);
            try
            {
                client4.ReceiveTimeout = int.Parse(Retry_milisec); ;

                client4.Connect(iPEnd);

                if (client4.Connected)
                {
                    STR = new StreamReader(client4.GetStream());
                    STW = new StreamWriter(client4.GetStream());
                    STW.AutoFlush = true;

                    STW.WriteLine("Hi Server");

                    IsClient4_Connected = true;
                    IsConnected = "connected";
                    Insert_Connectivity_Status("C4", "Connected"); // Function calling  (Connectivity Status)

                    if (DebugLog == true)
                    {
                        WriteToLogFile("client 4 Connected: " + DateTime.Now);
                    }

                    while (IsClient4_Connected)
                    {
                        cmd = "";
                        cmd = STR.ReadLine();

                        if (DebugLog == true)
                        {
                            WriteToLogFile("T4 cmd: " + cmd);
                        }
                        /************************************/

                        if (cmd.Length > 0)
                        {
                            if (cmd.StartsWith("|ID-") && (cmd.EndsWith("%"))) //RFID tag command 
                            {
                                cmd = cmd.Remove(0, 4).Replace("%", "");

                                if (cmd.Length > 0)
                                {
                                    Check_Tag_Valid_Invalid(cmd); // Function calling  (Check Tag is valid or not and also check gross is available or not->  OPEN, INVALID)
                                }
                                else
                                {
                                    STW.WriteLine("|INVALID%");
                                    Send_Audio_Message("TAG " + cmd, "Unauthorised Vehicle");
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
                    WriteToLogFile("Client4 retry is on: " + DateTime.Now);
                }

                if (IsConnected.Equals("connected"))
                {
                    IsConnected = "NotConncted";
                    Insert_Connectivity_Status("C4", "Disconnected");
                }

                IsClient4_Connected = false;

                if (client_four != null)
                    client_four = null;

                client_four = new Thread(Client4);
                client_four.Start();
            }
        }

        /****************************** INSERT CONNECTIVITY STATUS (Client 4) Function ******************* |OK% *******************/
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
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();
                    Send_Audio_Message("TAG " + TagId, "Unauthorised Vehicle");
                }
                else
                {
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();
                    // If Tag is valid, then check gross available or not
                    Update_Logs_ExitDetails(vehicleNumber);
                }

                Reader.Close();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Check_Tag_Valid_Invalid_Exit Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** UPDATE GROSS WEIGHT Function ******************** |OPEN% **********************/
        public void Update_Logs_ExitDetails(string vehiclenumber)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            String currentDate = dateString;
            string grossWeight = "", printdatetime = "";
            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT grossweight, printdatetime from vehicleinoutlogs where vehiclenumber= '" + vehiclenumber + "' ORDER BY id DESC LIMIT 1";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    // MessageBox.Show(Reader.GetValue(0).ToString());
                    grossWeight = Reader["grossweight"].ToString();
                    printdatetime = Reader["printdatetime"].ToString();
                }
                Reader.Dispose();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile(ex.Message);
            }

            try
            {
                if (string.IsNullOrEmpty(grossWeight) || (Int32.Parse(grossWeight) < 0))
                {
                    // Gross Weight not found!
                    STW.WriteLine("|GROSSPENDING%");
                    WriteToErrorFile("Gross Weight not found of vehicle number: "+ vehiclenumber);
                    Insert_Voilation(vehiclenumber, "Gross weight pending of this vehicle, but it was come on exit gate."); // Insert voliation stage 4
                    Send_Audio_Message(vehicleNumber, "Authorise Vehicle. Gross weight not found. Go to for gross weight.");
                }
                else if(string.IsNullOrEmpty(printdatetime))
                {
                    STW.WriteLine("|GROSSPENDING%");
                    Send_Audio_Message(vehicleNumber, "Authorise Vehicle. Print Out Challan is required.");
                }
                else
                {
                    string stageno = "4", stagesync = "Pending";
                    int materialweight = (Int32.Parse(grossWeight) - Int32.Parse(grossWeight));
                    string Query = "UPDATE vehicleinoutlogs SET outdatetime= '" + currentDate + "', stageno= '" + stageno + "', stagesync= '" + stagesync + "' WHERE vehiclenumber= '" + vehiclenumber + "' ORDER BY id DESC LIMIT 1";
                    con = new MySqlConnection(DbPath.ToString());
                    MySqlCommand MyCommand2 = new MySqlCommand(Query, con);
                    MySqlDataReader MyReader2;
                    if (con == null)
                    {
                        con = new MySqlConnection(DbPath.ToString());
                    }
                    con.Open();
                    MyReader2 = MyCommand2.ExecuteReader();
                    STW.WriteLine("|OPEN%");
                    WriteToLogFile(vehiclenumber + "exit successfully");
                    MyCommand2.Dispose();
                    MyReader2.Dispose();
                    con.Close();//Connection closed here  
                    Send_Audio_Message(vehicleNumber, "Authorise Vehicle.");
                }
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Vehicle not exit: " + ex.Message);
            }
        }

        /****************************** INSERT VOILATION STAGE Function **************************************/
        public void Insert_Voilation(String vehiclenumber, string voilationMessage)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            String DateTimeis = dateString, voilationStage = "4", ticketStatus = "OPEN";

            try
            {
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                con.Open();
                MySqlCommand cmd = con.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "INSERT INTO voilation(vehilcenumber, voilationstage, message, created_at) values('" + vehiclenumber + "','" + voilationStage + "','" + voilationMessage + "','" + DateTimeis + "','" + ticketStatus + "')";
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

        /****************************** SEND AUDIO MESSAGE (INSERT/UPDATE) Function ******************************************/
        public void Send_Audio_Message(String vehilceNumber, String Message)
        {
            try
            {
                String audiomsgID = "", actionType = "OUT", actionPlay = "New";
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
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();
                    // Insert Audio Message
                    insertAudioMessage(actionType, vehilceNumber, Message, actionPlay);
                }
                else
                {
                    Reader.Close();
                    myComm.Dispose();
                    con.Close();
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
                }
                con.Open();
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
                string Query = "UPDATE audiomsg SET actiontype= '" + actionType + "', vehiclenumber= '" + vehilceNumber + "', msg = '" + Message + "', actionplay= '" + actionPlay + "' WHERE id= '1'";
                con = new MySqlConnection(DbPath.ToString());
                MySqlCommand MyCommand2 = new MySqlCommand(Query, con);
                MySqlDataReader MyReader2;
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                con.Open();
                MyReader2 = MyCommand2.ExecuteReader();
                if (DebugLog == true)
                {
                    WriteToLogFile("Audio Message Updated: " + Message);
                }
                MyCommand2.Dispose();
                MyReader2.Dispose();
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
