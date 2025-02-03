using Godot;
using System;

public class Global
{
    private static Global _instance;
    public static Global Instance
    {
        get
        {
            if (_instance == null)
                _instance = new Global();
            return _instance;
        }
    }

    public string Token { get; set; }
    public UserInfo UserInfo { get; set; }
} 