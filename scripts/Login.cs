using Godot;
using System;
using System.Text.Json;
using System.Threading.Tasks;

public partial class Login : Control
{
	private LineEdit _emailInput;
	private LineEdit _passwordInput;
	private Button _loginButton;
	private Button _registerButton;
	private Label _errorLabel;
	
	private HttpRequest _httpRequest;

	public override void _Ready()
	{
		// 更新节点路径以匹配新的场景结构
		_emailInput = GetNode<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Username");
		_passwordInput = GetNode<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Password");
		_loginButton = GetNode<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ButtonContainer/LoginButton");
		_registerButton = GetNode<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ButtonContainer/RegisterButton");
		_errorLabel = GetNode<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ErrorLabel");

		// 创建 HttpRequest 节点
		_httpRequest = new HttpRequest();
		AddChild(_httpRequest);
		
		// 清空错误信息
		_errorLabel.Text = "";

		// 连接登录按钮点击信号
		_loginButton.Pressed += OnLoginButtonPressed;
		_registerButton.Pressed += OnRegisterButtonPressed;
		
		// 连接 HTTP 请求完成信号
		_httpRequest.RequestCompleted += OnLoginRequestCompleted;
	}

	private void OnLoginButtonPressed()
	{
		string email = _emailInput.Text.Trim();
		string password = _passwordInput.Text;

		// 打印登录尝试
		GD.Print($"正在尝试登录: {email}");

		// 简单的输入验证
		if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
		{
			ShowError("请输入邮箱和密码");
			return;
		}

		if (!IsValidEmail(email))
		{
			ShowError("请输入有效的邮箱地址");
			return;
		}

		// 禁用登录按钮，防止重复点击
		_loginButton.Disabled = true;
		
		// 准备登录请求数据
		var loginData = new Godot.Collections.Dictionary
		{
			{ "email", email },
			{ "password", password }
		};
		
		string jsonData = Json.Stringify(loginData);
		GD.Print($"发送登录请求数据: {jsonData}");

		// 设置请求头
		string[] headers = new string[] { 
			"Content-Type: application/json",
			"Accept: application/json"
		};

		// 发送登录请求
		Error error = _httpRequest.Request(
			"http://localhost:3000/api/auth/login",
			headers,
			HttpClient.Method.Post,
			jsonData
		);

		if (error != Error.Ok)
		{
			ShowError("发送请求失败，请稍后重试");
			_loginButton.Disabled = false;
		}
	}

	private void OnRegisterButtonPressed()
	{
		// TODO: 实现注册功能或跳转到注册页面
		GD.Print("注册按钮被点击");
	}

	private void OnLoginRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
	{
		// 打印响应状态
		GD.Print($"收到响应: result={result}, responseCode={responseCode}");
		
		// 重新启用登录按钮
		_loginButton.Disabled = false;

		if (result != (long)HttpRequest.Result.Success)
		{
			GD.Print("请求失败: 网络错误");
			ShowError("网络请求失败，请检查网络连接");
			return;
		}

		if (responseCode != 200)
		{
			GD.Print($"请求失败: HTTP状态码 {responseCode}");
			ShowError("登录失败，请检查邮箱和密码");
			return;
		}

		try
		{
			// 打印原始响应数据
			GD.Print($"原始响应数据: {body.GetStringFromUtf8()}");

			// 解析响应数据
			var json = new Json();
			json.Parse(body.GetStringFromUtf8());
			var response = json.GetData().AsGodotDictionary();

			int code = (int)response["code"];
			GD.Print($"响应状态码: {code}");

			if (code == 200)
			{
				var data = response["data"].AsGodotDictionary();
				var userInfo = data["userInfo"].AsGodotDictionary();

				// 打印用户信息（注意不要打印敏感信息如token）
				GD.Print($"登录成功: 用户名={userInfo["username"]}, 评分={userInfo["rating"]}");

				// 保存token和用户信息
				Global.Instance.Token = (string)data["token"];
				Global.Instance.UserInfo = new UserInfo
				{
					userId = (string)userInfo["userId"],
					username = (string)userInfo["username"],
					rating = (int)userInfo["rating"]
				};

				// 显示成功消息
				ShowSuccess("登录成功！");
				
				// 跳转到主菜单场景
				GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
			}
			else
			{
				string message = response.ContainsKey("message") ? (string)response["message"] : "未知错误";
				GD.Print($"登录失败: {message}");
				ShowError("登录失败: " + message);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"解析响应数据失败: {e.Message}");
			GD.PrintErr($"异常堆栈: {e.StackTrace}");
			ShowError("数据解析错误");
		}
	}

	private void ShowError(string message)
	{
		_errorLabel.Modulate = Colors.Red;
		_errorLabel.Text = message;
	}

	private void ShowSuccess(string message)
	{
		_errorLabel.Modulate = Colors.Green;
		_errorLabel.Text = message;
	}

	private bool IsValidEmail(string email)
	{
		try
		{
			var addr = new System.Net.Mail.MailAddress(email);
			return addr.Address == email;
		}
		catch
		{
			return false;
		}
	}

	public override void _Process(double delta)
	{
	}
}

// 用于解析登录响应的类
public class LoginResponse
{
	public int code { get; set; }
	public string message { get; set; }
	public LoginData data { get; set; }
}

public class LoginData
{
	public string token { get; set; }
	public UserInfo userInfo { get; set; }
}

public class UserInfo
{
	public string userId { get; set; }
	public string username { get; set; }
	public int rating { get; set; }
} 
