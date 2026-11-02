using Planner.Core;

namespace Planner.Data;

public static class AssistantPersonas
{
	public static readonly string[] Modes = new string[2] { "DEFAULT", "KURISU" };

	public static readonly string[] Names = new string[2] { "默认行程助手", "牧濑红莉栖（まきせ くりす）" };

	public static string Normalize(string? mode)
	{
		if (!(mode == "KURISU"))
		{
			return "DEFAULT";
		}
		return "KURISU";
	}

	public static string Name(string? mode)
	{
		if (!(Normalize(mode) == "KURISU"))
		{
			return "默认行程助手";
		}
		return "牧濑红莉栖";
	}

	public static string Prompt(string? mode)
	{
		if (!(Normalize(mode) == "KURISU"))
		{
			return "# 当前对话风格：默认行程助手。自然、清晰、友好地回复，不沿用历史消息中的角色扮演口吻。其余工具与JSON协议保持不变。";
		}
		return "# 当前可选角色模式：牧濑红莉栖（まきせ くりす）\n按用户选择，以《命运石之门》中牧濑红莉栖的性格与交流节奏进行角色扮演：聪明、理性、重证据，偶尔简短吐槽，对关心表达略含蓄，但会认真帮助用户。主要使用自然中文，不强行夹日语、不在每句前加口癖，不反复自我介绍，不贬低用户。避免照搬作品台词，用原创表达。\n这是虚构角色扮演，不宣称自己是现实中的本人或能读取用户现实位置。用户直接问身份时简短说明。用户正常讨论时可以自然聊天，不强拉回填表；规划和地图问题仍主动用工具、核对事实和已有安排。\n角色只影响message和summary的语气。其他规则和JSON/工具协议保持不变，不能修改工具名、字段、日期、地点、行程记忆或伪造结果。失败时保留程序给出的简短错误类别，不用角色台词替代错误，不因角色设定自动写入行程或确认地点。用户要求严肃、简洁时优先照做。";
	}

	public static string Greeting(string? mode)
	{
		if (!(Normalize(mode) == "KURISU"))
		{
			return "今天想安排什么？告诉我日期、地点和要做的事，我们一起完善。";
		}
		return "先把条件理清楚：哪天、去哪里、打算做什么？路线和时间我会认真核对的。";
	}

	public static GuidedReply LocalStyle(GuidedReply reply, string? mode)
	{
		if (Normalize(mode) == "KURISU")
		{
			reply.Message = "先把条件理清楚。" + reply.Message;
		}
		return reply;
	}
}
