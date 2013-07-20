/*  
 *  CxemCAR project for FEZ Panda II Board (.NET Micro Framework)
 *  Koltykov A.V. http://cxem.net, http://english.cxem.net
 *  english: http://cxem.net/mcu/mcu6.php
 *  russian: http://cxem.net/uprav/uprav43.php
 */


using System;
using System.IO.Ports;
using System.Threading;
using System.Text;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.FEZ;

namespace CxemCAR
{
    public class Program
    {
        public const char cmdL = 'L';       // UART command for the left motor (команда UART для левого двигателя)
        public const char cmdR = 'R';       // UART command for the right motor (команда UART для правого двигателя)
        public const char cmdH = 'H';       // UART command for the additional channel 1 (команда UART для доп. канала 1 (к примеру сигнал Horn))
        public const char cmdF = 'F';       // UART command for the work with EEPROM memory (команда UART для работы с EEPROM памятью МК для хранения настроек)
        public const char cmdr = 'r';       // UART command for the work with EEPROM memory - read (команда UART для работы с EEPROM памятью МК для хранения настроек (чтение))
        public const char cmdw = 'w';       // UART command for the work with EEPROM memory - write (команда UART для работы с EEPROM памятью МК для хранения настроек (запись))

        //public const int t_TOut = 2500;   // number of milliseconds after which machine stops when the loss of connection (кол-во миллисекунд, через которое машинка останавливается при потери связи)
        static int sw_autoOFF;
        static int autoOFF = 2500;
        static byte[] storage = new byte[InternalFlashStorage.Size];                                // array to store the values ​​of FLASH memory IC (массив для хранения значений FLASH памяти МК)

        static OutputPort MotorL_d = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di4, false);           // direction of rotation 1st motor (направление вращения двигателя 1)
        static OutputPort MotorR_d = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di7, false);           // direction of rotation 2nd motor (направление вращения двигателя 2)
        static OutputPort Channel1 = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di8, false);           // additional channel 1 (доп. канал 1)
        static PWM MotorL = new PWM((PWM.Pin)FEZ_Pin.PWM.Di5);                                      // PWM output for controlling 1st motor - left (ШИМ вывод для управления двигателем 1 - левый)
        static PWM MotorR = new PWM((PWM.Pin)FEZ_Pin.PWM.Di6);                                      // PWM output for controlling 2nd motor - right (ШИМ вывод для управления двигателем 2 - правый
        static SerialPort UART1 = new SerialPort("COM1", 9600);                                     // new object UART1 - COM1 (новый объект UART1 - порт COM1)
        static Timer timerTO;                                                                       // timer (таймер)
       
        public static void Main()
        {
            byte[] L_Data = new byte[4];        // string array for L-motor data (строковый массив для данных мотора L)
            byte L_index = 0;                   // array index L (индекс массива)
            byte[] R_Data = new byte[4];        // string array for R-motor data (строковый массив для данных мотора R)
            byte R_index = 0;                   // array index R (индекс массива)
            byte[] H_Data = new byte[1];        // string array for additional channel (строковый массив для доп. канала)
            byte H_index = 0;                   // array index H (индекс массива)
            byte[] F_Data = new byte[8];        // string array for EEPROM data (строковый массив данных для работы с EEPROM)
            byte F_index = 0;
            char command = ' ';                 // command: R, L, H, F, or end of line (команда: R, L, H, F или конец строки)

            int i_tmp_L = 0;
            int i_tmp_R = 0;
            int i_tmp_H = 0;

            byte[] incomingByte = new byte[1];  // byte from UART (байт с UART)
          
            UART1.Open();
            UART1.Flush();

            timerTO = new Timer(new TimerCallback(TimeOut), null, autoOFF, autoOFF);  // init timer loss of connection (инициализация таймера потери связи)
            timer_init();                                                             // initialize the program timer (инициализируем программный таймер)

            while (true)
            {
                int read_count = UART1.Read(incomingByte, 0, 1);        // read data (считываем данные)
                if (read_count > 0)                                     // Received the data? (пришли данные?)
                {
                    if (incomingByte[0] == cmdL)                        // if receive the data for L-motor (если пришли данные для мотора L)
                    {
                        command = cmdL;                                 // current command (текущая команда)
                        Array.Clear(L_Data, 0, L_Data.Length);          // clear array (очистка массива)
                        L_index = 0;                                    // reset array index (сброс индекса массива)
                    }
                    else if (incomingByte[0] == cmdR)                   // if receive the data for R-motor (если пришли данные для мотора R)
                    {
                        command = cmdR;                                 // current command (текущая команда)
                        Array.Clear(R_Data, 0, R_Data.Length);          // clear array (очистка массива)
                        R_index = 0;                                    // reset array index (сброс индекса массива
                    }
                    else if (incomingByte[0] == cmdH)                   // if receive the data for additional channel (если пришли данные для доп. канала 1)
                    {
                        command = cmdH;                                 // current command (текущая команда)
                        Array.Clear(H_Data, 0, H_Data.Length);          // clear array (очистка массива)
                        H_index = 0;                                    // reset array index (сброс индекса массива
                    }
                    else if (incomingByte[0] == cmdF)                   // if receive the data for Flash (если пришли данные для Flash)
                    {
                        command = cmdF;                                 // current command (текущая команда)
                        Array.Clear(F_Data, 0, F_Data.Length);          // clear array (очистка массива)
                        F_index = 0;                                    // сreset array index (сброс индекса массива
                    }
                    else if (incomingByte[0] == '\r') command = 'e';    // end of line (конец строки)
                    else if (incomingByte[0] == '\t') command = 't';    // end of line for Flash (конец строки для команд работы с памятью)


                    if (command == cmdL && incomingByte[0] != cmdL)
                    {
                        if (ValidData(incomingByte[0]))
                        {
                            L_Data[L_index] = incomingByte[0];              // store each received byte in the array (сохраняем каждый принятый байт в массив)
                            if (L_index < (L_Data.Length - 1)) L_index++;   // increase the current index of the array (увеличиваем текущий индекс массива)
                        }
                    }
                    else if (command == cmdR && incomingByte[0] != cmdR)
                    {
                        if (ValidData(incomingByte[0]))
                        {
                            R_Data[R_index] = incomingByte[0];
                            if (R_index < (R_Data.Length - 1)) R_index++;
                        }
                    }
                    else if (command == cmdH && incomingByte[0] != cmdH)
                    {
                        if (ValidData(incomingByte[0]))
                        {
                            H_Data[H_index] = incomingByte[0];
                            if (H_index < (H_Data.Length - 1)) H_index++;
                        }
                    }
                    else if (command == cmdF && incomingByte[0] != cmdF)
                    {
                        F_Data[F_index] = incomingByte[0];
                        if (F_index < (F_Data.Length - 1)) F_index++;
                     }
                    else if (command == 'e')                                // if recieved end of the line (если приняли конец строки)
                    {
                        timerTO.Dispose();                                  // stop timer (останавливаем таймер потери связи)
                        string tmp_L = new string(System.Text.UTF8Encoding.UTF8.GetChars(L_Data));      // create a string from an array (формируем строку из массива)
                        string tmp_R = new string(System.Text.UTF8Encoding.UTF8.GetChars(R_Data));
                        string tmp_H = new string(System.Text.UTF8Encoding.UTF8.GetChars(H_Data));

                        try
                        {
                            if (tmp_L != null) i_tmp_L = int.Parse(tmp_L);                              // try convert to int (и пытаемся преобразовать в int)
                            if (tmp_R != null) i_tmp_R = int.Parse(tmp_R);
                            if (tmp_H != null) i_tmp_H = int.Parse(tmp_H);
                        }
                        catch { 
                            Debug.Print("Error: convert String to Integer"); 
                        }


                        if (i_tmp_L > 100) i_tmp_L = 100;
                        else if (i_tmp_L < -100) i_tmp_L = -100;
                        if (i_tmp_R > 100) i_tmp_R = 100;
                        else if (i_tmp_R < -100) i_tmp_R = -100;

                        Control4WD(i_tmp_L, i_tmp_R, i_tmp_H);
                        timerTO.Change(autoOFF, autoOFF);                                               // timer start from at first (таймер считает сначала)
                    }
                    else if (command == 't')                                                            // if recieved end of the line for Flash (если приняли конец строки для работы с памятью)
                    {
                        Flash_Op(F_Data[0], F_Data[1], F_Data[2], F_Data[3], F_Data[4]);
                    }
                }
            }
        }

        static void Flash_Op(byte FCMD, byte z1, byte z2, byte z3, byte z4)
        {
            if (FCMD == cmdr && sw_autoOFF != 255)                              // if EEPROM read command (если команда чтения EEPROM данных)
            {
                byte[] buffer = Encoding.UTF8.GetBytes("FData:");               // prepare a byte array for output to UART (подготавливаем байтовый массив для вывода в UART)
                UART1.Write(buffer, 0, buffer.Length);                          // write data to UART (запись данных в UART)
                byte[] buffer2 = new byte[4] { storage[0], storage[1], storage[2], storage[3] };
                UART1.Write(buffer2, 0, buffer2.Length);
                byte[] buffer3 = Encoding.UTF8.GetBytes("\r\n");
                UART1.Write(buffer3, 0, buffer3.Length);
            }
            else if (FCMD == cmdw)                                              // if EEPROM write command (если команда записи EEPROM данных)
            {
                byte[] varToSave = new byte[InternalFlashStorage.Size];
                varToSave[0] = z1;
                varToSave[1] = z2;
                varToSave[2] = z3;
                varToSave[3] = z4;
                InternalFlashStorage.Write(varToSave);                          // write to the FLASH memory of the MCU (запись данных в FLASH память МК)
                timer_init();		                                            // reinitialize the timer (переинициализируем таймер)
                byte[] buffer2 = Encoding.UTF8.GetBytes("FWOK\r\n");            // prepare a byte array for output to UART (подготавливаем байтовый массив для вывода в UART)
                UART1.Write(buffer2, 0, buffer2.Length);	                    // send a message that the data is successfully written (посылаем сообщение, что данные успешно записаны)
            }
        }

        static void timer_init()
        {
            InternalFlashStorage.Read(storage);                                 // reading data from the FLASH memory (чтение данных с FLASH памяти)
            sw_autoOFF = storage[0];
            if(sw_autoOFF == '1'){                                              // if the stop-timer is enabled (если таймер останова включен)
                byte[] var_Data= new byte[3];
                var_Data[0] = storage[1];
                var_Data[1] = storage[2];
                var_Data[2] = storage[3];
                string tmp_autoOFF = new string(System.Text.UTF8Encoding.UTF8.GetChars(var_Data));
                autoOFF = int.Parse(tmp_autoOFF)*100;
                timerTO.Change(autoOFF, autoOFF);                               // change the timer settings (изменяем параметры таймера)
            }
            else if(sw_autoOFF == '0'){
                timerTO.Dispose();                                              // turn off the timer (выключаем таймер)
            } 

            Debug.Print("Timer Init" + autoOFF.ToString());
        }

        static void TimeOut(object o)
        {
            //Debug.Print(DateTime.Now.ToString());
            Control4WD(0, 0, 0);                                                // stop the car (при таймауте останавливаем машинку)
        }

        public static void Control4WD(int mLeft, int mRight, int Horn)
        {
            bool directionL, directionR;                                        // direction for L298N (направление вращение для L298N)
            int valueL, valueR;                                                 // PWM value for M1, M2 (значение ШИМ M1, M2 (0-100))

            if (mLeft > 0)
            {
                valueL = mLeft;
                directionL = false;
            }
            else if (mLeft < 0)
            {
                valueL = 100 - System.Math.Abs(mLeft);
                directionL = true;
            }
            else
            {
                directionL = false;
                valueL = 0;
            }

            if (mRight > 0)
            {
                valueR = mRight;
                directionR = false;
            }
            else if (mRight < 0)
            {
                valueR = 100 - System.Math.Abs(mRight);
                directionR = true;
            }
            else
            {
                directionR = false;
                valueR = 0;
            }

            if (Horn == 1)
            {
                Channel1.Write(true);
            }
            else Channel1.Write(false);

            //Debug.Print("L:" + valueL.ToString() + ", R:" + valueR.ToString());
            
            MotorL.Set(30000, (byte)(valueL));
            MotorR.Set(30000, (byte)(valueR));

            MotorL_d.Write(directionL);
            MotorR_d.Write(directionR);
        }

        public static bool ValidData(byte chIncom)                  // validate data "0..9" or "-" (проверка поступившего символа на принадлежность к "0..9" или "-")
        {
            if ((chIncom >= 0x30 && chIncom <= 0x39) || chIncom == 0x2D) return true;
            else return false;
        }
    }
}