using System.Collections.Generic;

namespace ActRoguelike.Config;

/// <summary>内容定义自己报告参数错误；统一校验器无需识别每一种具体定义。</summary>
public interface IContentValidatable
{
	IEnumerable<string> Validate();
}
