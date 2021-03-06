﻿using System;

namespace ModExtenderCommunication
{
    public enum ClientHostCommand
    {
        Game_Exit,
        RestartHost,
        Game_Update,
        Console_Write,
        ExposeShutdownHost,
        Ping
    }

    [Serializable]
    public class ClientHostComData
    {
        public ClientHostCommand Command { get; set; }
        public object Data { get; set; }
    }

}
