# InspectorButton

Unityのインスペクターに表示されるボタンを追加できるエディタ拡張

<img width="456" height="260" alt="Image" src="https://github.com/user-attachments/assets/d3b20022-414d-4863-9a40-5ee56381fd2a" />

## 追加方法

1. Unity の Package Manager を開く
1. 右上の [+] メニューから「Install package from git URL...」を選択
1. 次の URL を貼り付けてインストール

```text
https://github.com/orisaki888/InspectorButton.git
```

## 使い方

属性を付けたメソッドをインスペクターにボタンとして表示します。引数つきメソッドにも対応し、インスペクター上で値を入力してから呼び出せます。

### 基本

```csharp
using UnityEngine;

public class ButtonSample : MonoBehaviour
{

    // メソッド名をニックネーム化してボタン名に（例: Say Hello）
    [Button]
    private void SayHello()
    {
        Debug.Log("Hello, World!");
    }

    // ボタンの表示名を指定
    [Button("Multiply Numbers")]
    public void Multiply(int a, int b)
    {
        Debug.Log($"Multiply: {a} * {b} = {a * b}");
    }

    // シリアライズ可能なクラスの引数も描画されます
    [System.Serializable]
    public class MySerializableClass
    {
        public int myInt;
        public float myFloat;
        public string myString;
    }

    [Button("Show Serializable Class Info")]
    public void ShowSerializableClassInfo(MySerializableClass myClass)
    {
        if (myClass != null)
        {
            Debug.Log($"MySerializableClass Info - Int: {myClass.myInt}, Float: {myClass.myFloat}, String: {myClass.myString}");
        }
        else
        {
            Debug.Log("MySerializableClass instance is null.");
        }
    }
}
```

ポイント:

- [Button] 属性はメソッドに付けます。アクセス修飾子は問いません（public/private/protected OK）。
- 引数がある場合はメソッド名の下に折りたたみ（Foldout）が表示され、値を入力してからボタンを押します。
- static メソッドは一度だけ実行されます。インスタンス メソッドは選択中オブジェクト全てに対して実行されます（複数選択対応）。
- エディットモードでも実行できます。シーンやオブジェクトが変更された場合は Undo/Redo に対応し、シーンは Dirty マークされます。

### 対象（どの型でボタンが出るか）

- 既定: MonoBehaviour を継承したコンポーネントに対してボタンを表示します。
- すべての UnityEngine.Object（ScriptableObject など）でも使いたい場合は、スクリプト定義シンボルに `INSPECTOR_BUTTON_ALL_TARGETS` を追加してください。
  - 手順: Project Settings > Player > Other Settings > Scripting Define Symbols に `INSPECTOR_BUTTON_ALL_TARGETS` を追記して保存。

## サポートされる引数型

次の型はインスペクターで編集できます（配列/`List<T>` も要素型がサポートされていれば可）。

- プリミティブ: int, float, double, bool, string
- ベクトル/構造体: Vector2, Vector3, Vector4, Vector2Int, Vector3Int, Color, Rect, Bounds
- その他: AnimationCurve, Gradient, LayerMask, Enum（Flags も対応）, UnityEngine.Object 派生（ObjectField）
- 配列/リスト: `T[]` / `List<T>`（T が上記いずれか・または下記のプレーンなシリアライズ型）
- プレーンなシリアライズ可能型: `[Serializable]` が付いたクラス/構造体で、Unity のオブジェクト派生でないもの
  - 対応フィールド: public フィールド、または `[SerializeField]` が付いた非公開フィールド

デフォルト値: メソッド引数にデフォルト値が設定されていればそれを利用します。なければ値型は既定値、参照型は null から開始します。

## 注意事項 / 制限

- base クラスに定義したメソッドは表示されません（現状、同一クラスで宣言されたメソッドのみを対象にしています）。
- `ref`/`out` 引数は非対応です。
- ジェネリック メソッドは非対応です。
- サポート外の型を引数に含むメソッドは「Unsupported」として扱われ、ボタンは無効化または表示されません。
- 既にその型に専用の CustomEditor がある場合、これは「フォールバックエディタ」のため置き換えられず、ボタンが表示されません。その場合は、独自の CustomEditor に同等の描画を組み込む必要があります。
- エディットモードで実行するとシーンを変更しうるため、プロジェクトの状態に注意してください（大きな処理はプレイモードでの実行を推奨）。
