﻿/*
 * Copyright 2015 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : ScadaCommCommon
 * Summary  : Serial port communication channel logic
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2015
 * Modified : 2015
 */

using Scada.Comm.Devices;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace Scada.Comm.Channels
{
    /// <summary>
    /// Serial port communication channel logic
    /// <para>Логика работы канала связи через последовательный порт</para>
    /// </summary>
    public class CommSerialLogic : CommChannelLogic
    {
        /// <summary>
        /// Настройки канала связи
        /// </summary>
        public class Settings
        {
            /// <summary>
            /// Конструктор
            /// </summary>
            public Settings()
            {
                // установка значений по умолчанию
                PortName = "COM1";
                BaudRate = 9600;
                Parity = Parity.None;
                DataBits = 8;
                StopBits = StopBits.One;
                DtrEnable = false;
                RtsEnable = false;
                Behavior = CommChannelLogic.OperatingBehaviors.Master;
            }

            /// <summary>
            /// Получить или установить имя последовательного порта
            /// </summary>
            public string PortName { get; set; }
            /// <summary>
            /// Получить или установить скорость обмена
            /// </summary>
            public int BaudRate { get; set; }
            /// <summary>
            /// Получить или установить контроль чётности
            /// </summary>
            public Parity Parity { get; set; }
            /// <summary>
            /// Получить или установить биты данных
            /// </summary>
            public int DataBits { get; set; }
            /// <summary>
            /// Получить или установить стоповые биты
            /// </summary>
            public StopBits StopBits { get; set; }
            /// <summary>
            /// Получить или установить использование сигнала Data Terminal Ready (DTR) 
            /// </summary>
            public bool DtrEnable { get; set; }
            /// <summary>
            /// Получить или установить использование сигнала Request to Send (RTS)
            /// </summary>
            public bool RtsEnable { get; set; }
            /// <summary>
            /// Получить или установить режим работы канала связи
            /// </summary>
            public OperatingBehaviors Behavior { get; set; }
        }


        /// <summary>
        /// Настройки канала связи
        /// </summary>
        protected Settings settings;
        /// <summary>
        /// Соединение через последовательный порт
        /// </summary>
        protected SerialConnection serialConn;


        /// <summary>
        /// Конструктор
        /// </summary>
        public CommSerialLogic()
            : base()
        {
            settings = new Settings();
            serialConn = null;
        }


        /// <summary>
        /// Получить наименование канала связи
        /// </summary>
        public override string InternalName
        {
            get
            {
                return "CommSerialPort";
            }
        }

        /// <summary>
        /// Получить режим работы
        /// </summary>
        public override OperatingBehaviors Behavior
        {
            get
            {
                return settings.Behavior;
            }
        }


        /// <summary>
        /// Обработать событие приёма данных по последовательному порту
        /// </summary>
        protected void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            KPLogic targetKP = null;
            if (!ExecProcUnreadIncomingReq(kpList[0], serialConn, ref targetKP))
                serialConn.DiscardInBuffer();
        }


        /// <summary>
        /// Инициализировать канал связи
        /// </summary>
        public override void Init(SortedList<string, string> commCnlParams, List<KPLogic> kpList)
        {
            // вызов метода базового класса
            base.Init(commCnlParams, kpList);

            // получение настроек канала связи
            settings.PortName = GetStringParam(commCnlParams, "PortName", true, settings.PortName);
            settings.BaudRate = GetIntParam(commCnlParams, "BaudRate", true, settings.BaudRate);
            settings.Parity = GetEnumParam<Parity>(commCnlParams, "Parity", false, settings.Parity);
            settings.DataBits = GetIntParam(commCnlParams, "DataBits", false, settings.DataBits);
            settings.StopBits = GetEnumParam<StopBits>(commCnlParams, "StopBits", false, settings.StopBits);
            settings.DtrEnable = GetBoolParam(commCnlParams, "DtrEnable", false, settings.DtrEnable);
            settings.RtsEnable = GetBoolParam(commCnlParams, "RtsEnable", false, settings.RtsEnable);
            settings.Behavior = GetEnumParam<OperatingBehaviors>(commCnlParams, "Behavior",
                false, settings.Behavior);

            // создание клиента и соединения
            SerialPort serialPort = new SerialPort(settings.PortName, settings.BaudRate, settings.Parity, 
                settings.DataBits, settings.StopBits) { DtrEnable = settings.DtrEnable, RtsEnable = settings.RtsEnable };
            serialConn = new SerialConnection(serialPort);

            // установка соединения всем КП на линии связи
            foreach (KPLogic kpLogic in kpList)
            {
                kpLogic.Connection = serialConn;
                kpLogic.SerialPort = serialPort;
            }

            // проверка библиотек КП в режиме ведомого
            string warnMsg;
            if (settings.Behavior == OperatingBehaviors.Slave && !AreDllsEqual(out warnMsg))
                WriteToLog(warnMsg);
        }

        /// <summary>
        /// Запустить работу канала связи
        /// </summary>
        public override void Start()
        {
            // попытка открыть последовательный порт
            serialConn.Open();

            // привязка события приёма данных в режиме ведомого
            if (settings.Behavior == OperatingBehaviors.Slave && kpList.Count > 0)
                serialConn.SerialPort.DataReceived += serialPort_DataReceived;
        }

        /// <summary>
        /// Остановить работу канала связи
        /// </summary>
        public override void Stop()
        {
            // отключение события приёма данных в режиме ведомого
            serialConn.SerialPort.DataReceived -= serialPort_DataReceived;

            // очистка ссылки на соединение для всех КП на линии связи
            foreach (KPLogic kpLogic in kpList)
                kpLogic.Connection = null;

            // закрытие последовательного порта
            serialConn.Close();
        }

        /// <summary>
        /// Получить информацию о работе канала связи
        /// </summary>
        public override string GetInfo()
        {
            StringBuilder sbInfo = new StringBuilder(base.GetInfo());

            if (Localization.UseRussian)
            {
                sbInfo.Append("Последовательный порт: ");                
                if (serialConn == null)
                    sbInfo.Append("не определён");
                else
                    sbInfo.Append(serialConn.SerialPort.PortName)
                        .Append(serialConn.SerialPort.IsOpen ? " (открыт)" : " (закрыт)");
            }
            else
            {
                sbInfo.Append("Serial port: ");
                if (serialConn == null)
                    sbInfo.Append("undefined");
                else
                    sbInfo.Append(serialConn.SerialPort.PortName)
                        .Append(serialConn.SerialPort.IsOpen ? " (open)" : " (closed)");
            }

            return sbInfo.ToString();
        }
    }
}
