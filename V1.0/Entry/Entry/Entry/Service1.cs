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
        public static String Server1_IP = "", Retry_milisec = "";
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
                while (((line = myfile.ReadLine()) != null) || (count < 4))
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

        /*******************************Client 1************************************/
        public void Client1()
        {
            //  Thread.Sleep(20000);
            client = new TcpClient();
            // IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(Server1_IP), 80);
            IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(Server1_IP), 23);
            try
            {
                client.ReceiveTimeout = int.Parse(Retry_milisec); ;

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
                            else if (cmd.StartsWith("|ID-") && (cmd.EndsWith("%"))) //RFID tag command 
                            {
                                cmd = cmd.Remove(0, 4).Replace("%", "");

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
                Thread.Sleep(int.Parse(Retry_milisec));
                if (DebugLog == true)
                {
                    WriteToLogFile("Client1 retry is on: " + DateTime.Now);
                }
                Insert_Connectivity_Status("C1", "Disconnected");


                if (IsConnected.Equals("connected"))
                {
                    IsConnected = "NotConncted";
                    Insert_Connectivity_Status("C1", "Disconnected");
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

        /****************************** CHECK TAG VALID INVALID Function ******************** |INVALID% **********************/
        public void Check_Tag_Valid_Invalid_TareWeight(String TagId)
        {
            try
            {
                String vehicleNumber = "", TareType = "";
                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                    con.Open();
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT vehiclenumber, teirtype FROM vehicles WHERE rfidtagid= '" + TagId + "'";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    vehicleNumber = Reader["vehiclenumber"].ToString();
                    TareType = Reader["teirtype"].ToString();
                    if (DebugLog == true)
                    {
                        WriteToLogFile("vehicleNumber is:" + vehicleNumber + " and TareType is: "+ TareType);
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

                    // check validation- This vehicle expires or not
                    Check_Vehicle_POLLU_INS_RC_FITNESS(vehicleNumber);
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

        /****************************** CHECK THIS TAG TARE WEIGHT Function *******************************/
        public void Check_Vehicle_POLLU_INS_RC_FITNESS(String vehicleNumber)
        {
            try
            {
                var dateString = DateTime.Now.ToString("yyyy-MM-dd");
                DateTime currentDate = DateTime.ParseExact(dateString, "yyyy-MM-dd", null);

                String insuranceexpirydate = "", pollutionexpirydate = "", rcexpirydate = "", fitnessexpirydate = "";

                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                myComm.CommandText = "SELECT insuranceexpirydate, pollutionexpirydate, rcexpirydate, fitnessexpirydate FROM vehicles WHERE vehiclenumber = '" + vehicleNumber + "' ORDER BY id DESC LIMIT 1";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    insuranceexpirydate = Reader["insuranceexpirydate"].ToString();
                    pollutionexpirydate = Reader["pollutionexpirydate"].ToString();
                    rcexpirydate = Reader["rcexpirydate"].ToString();
                    fitnessexpirydate = Reader["fitnessexpirydate"].ToString();
                }
                /*** Check Insurance Expire or Not ***/
                DateTime insuranceexpirydate_dtformat = DateTime.ParseExact(insuranceexpirydate, "yyyy-MM-dd", null);
                TimeSpan ins_value = insuranceexpirydate_dtformat.Subtract(currentDate);
                double insurance_days = ins_value.Days;

                /*** Check Pollution Expire or Not ***/
                DateTime pollutionexpirydate_dtformat = DateTime.ParseExact(pollutionexpirydate, "yyyy-MM-dd", null);
                TimeSpan pollu_value = pollutionexpirydate_dtformat.Subtract(currentDate);
                double pollution_days = pollu_value.Days;

                /*** Check RC Expire or Not ***/
                DateTime rcexpirydate_dtformat = DateTime.ParseExact(rcexpirydate, "yyyy-MM-dd", null);
                TimeSpan rc_value = rcexpirydate_dtformat.Subtract(currentDate);
                double rc_days = rc_value.Days;

                /*** Check Fitness Expire or Not ***/
                DateTime fitnessexpirydate_dtformat = DateTime.ParseExact(fitnessexpirydate, "yyyy-MM-dd", null);
                TimeSpan fitness_value = fitnessexpirydate_dtformat.Subtract(currentDate);
                double fitness_days = fitness_value.Days;

                if (insurance_days <= 0)
                {
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Insurance Expire of Vehicle Number " + vehicleNumber);
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    Send_Audio_Message(vehicleNumber, "Insurance Expire of Vehicle Number " + vehicleNumber);
                }
                else if (pollution_days <= 0)
                {
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Pollution Expire of Vehicle Number " + vehicleNumber);
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    Send_Audio_Message(vehicleNumber, "Pollution Expire of Vehicle Number " + vehicleNumber);
                }
                else if (rc_days <= 0)
                {
                    if (DebugLog == true)
                    {
                        WriteToLogFile("RC Expire of Vehicle Number " + vehicleNumber);
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    Send_Audio_Message(vehicleNumber, "RC Expire of Vehicle Number " + vehicleNumber);
                }
                else if (fitness_days <= 0)
                {
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Fitness Expire of Vehicle Number " + vehicleNumber);
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    Send_Audio_Message(vehicleNumber, "Fitness Expire of Vehicle Number " + vehicleNumber);
                }
                else
                {
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Validation Checked - Vehicle no: "+vehicleNumber);
                    }

                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();

                    // check - This vehicle entry exists or not
                    Check_Entry_Exists_OR_Not(vehicleNumber);
                }
                Reader.Dispose();
                myComm.Dispose();
                con.Close();
            }
            catch (Exception ex)
            {
                con.Close();
                WriteToErrorFile("Check_Vehicle_POLLU_INS_RC_FITNESS Exception : " + DateTime.Now + "  " + ex.Message.ToString());
            }
        }

        /****************************** CHECK THIS TAG TARE WEIGHT Function ************** |OPEN% *********** |BLINK% *****************/
        public void Check_Entry_Exists_OR_Not(String vehicleNumber)
        {
            try
            {
                String inDateTime_existsornot = "";

                if (con == null)
                {
                    con = new MySqlConnection(DbPath.ToString());
                }
                MySqlCommand myComm = con.CreateCommand();
                MySqlDataReader Reader;
                //myComm.CommandText = "SELECT indatetime FROM vehicleinoutlogs WHERE vehiclenumber = '" + vehicleNumber + "' AND grossweight = '' ORDER BY id DESC LIMIT 1";
                myComm.CommandText = "SELECT indatetime FROM vehicleinoutlogs WHERE vehiclenumber = '" + vehicleNumber + "' AND teirweight = '' AND grossweight = '' AND outdatetime = '' ORDER BY id DESC LIMIT 1";
                con.Open();
                Reader = myComm.ExecuteReader();
                while (Reader.Read())
                {
                    inDateTime_existsornot = Reader["indatetime"].ToString();
                }
                if (string.IsNullOrEmpty(inDateTime_existsornot))
                {
                    // Vehicle Entry Not Found
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Vehicle Entry Not Exists");
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();

                    // check Tare Weight and Insert Log
                    Check_TareWeight(vehicleNumber, true);
                    
                }
                else
                {
                    // Vehicle Entry Found
                    if (DebugLog == true)
                    {
                        WriteToLogFile("Already Vehicle Entry Exists");
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    // check Only Tare Weight
                    Check_TareWeight(vehicleNumber, false);
                    Send_Audio_Message(vehicleNumber, "Authorise Vehicle. Vehicle Trip Starts");
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


        /****************************** CHECK THIS TAG TARE WEIGHT Function ************** |OPEN% *********** |BLINK% *****************/
        public void Check_TareWeight(String vehicleNumber, Boolean Entry_Available)
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
                // myComm.CommandText = "SELECT teirweight, teirweightdatetime from vehicleinoutlogs where vehiclenumber= '" + vehicleNumber + "' AND DATE(teirweightdatetime) = DATE('" + currentDate + "') AND teirweight > 0 ORDER BY id DESC LIMIT 1";
                myComm.CommandText = "SELECT vehicleinoutlogs.teirweight, vehicleinoutlogs.teirweightdatetime from vehicleinoutlogs INNER JOIN vehicles ON vehicles.vehiclenumber = vehicleinoutlogs.vehiclenumber where vehicles.teirtype = 'Single' AND vehicleinoutlogs.vehiclenumber = '" + vehicleNumber + "' AND DATE(vehicleinoutlogs.teirweightdatetime) = DATE('" + currentDate + "') AND vehicleinoutlogs.teirweight > 0 ORDER BY vehicleinoutlogs.id DESC LIMIT 1";
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
                        WriteToLogFile("Tare Weight Not Found - |BLINK%");
                    }
                    Reader.Dispose();
                    myComm.Dispose();
                    con.Close();
                    if (Entry_Available)
                    {
                        Insert_Vehicle_Logs(vehicleNumber, "", "", "1", "Pending");
                    }

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
                    if (Entry_Available)
                    {
                        Insert_Vehicle_Logs(vehicleNumber, tareWeight, tareWeightDateTime, "2", "Pending");
                    }

                    Send_Audio_Message(vehicleNumber, "Authorise Vehicle. Go to gross weight.");
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
            var dateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                cmd.CommandText = "INSERT INTO vehicleinoutlogs(vehiclenumber, indatetime, teirweight, teirweightdatetime, grossweight, grossdatetime, materialweight, outdatetime, po_number, lineweight, materialtype, challannumber, printdatetime, stageno, stagesync, created_at) " +
                                                                "values('"+ vehicleNumber + "', '"+ DateTimeis + "', '" + tareWeight + "', '" + teirweightdatetime + "', '" + grossweight + "', '" + grossdatetime + "', '" + materialweight + "', '" + outdatetime + "', '" + po_number + "', '" + lineweight + "', '" + materialtype + "', '" + challannumber + "', '" + printdatetime + "', '" + stageno + "', '" + stagesync + "', '" + DateTimeis + "')"; 
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
