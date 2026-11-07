using TrayBright;

using var mutex = new Mutex(true, "TrayBright_SingleInstance", out bool createdNew);
if (!createdNew)
    return; // 已有实例在运行

ApplicationConfiguration.Initialize();
Application.Run(new TrayAppContext());
