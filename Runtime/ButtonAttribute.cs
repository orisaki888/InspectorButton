using System;

/// <summary>
/// メソッドをインスペクターにボタンとして表示するための属性です。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class ButtonAttribute : Attribute
{
    /// <summary>
    /// ボタンに表示されるテキスト。nullまたは空の場合、メソッド名が使用されます。
    /// </summary>
    public string ButtonName { get; private set; }

    /// <summary>
    /// メソッドをインスペクターにボタンとして表示します。
    /// </summary>
    /// <param name="buttonName">オプション。ボタンに表示する名前。</param>
    public ButtonAttribute(string buttonName = null)
    {
        ButtonName = buttonName;
    }
}