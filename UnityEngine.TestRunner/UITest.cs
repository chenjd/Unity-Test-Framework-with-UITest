using UnityEngine;
using System.Collections;
using System;
using System.IO;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UITest
{
    const float WaitTimeout = 2;
    const float WaitIntervalFrames = 10;
    static MonoBehaviour mb;

    public class MB : MonoBehaviour
    {
    }

    protected void CreateMonoBehaviour()
    {
        if (mb == null)
        {
            var go = new GameObject("mb");
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            mb = go.AddComponent<MB>();            
        }
    }

    protected Coroutine StartCoroutine(IEnumerator enumerator)
    {
        CreateMonoBehaviour();
        return mb.StartCoroutine(enumerator);
    }

    protected Coroutine WaitFor(Condition condition)
    {
        return StartCoroutine(WaitForInternal(condition, Environment.StackTrace));
    }
                
    protected Coroutine LoadScene(string name)
    {
        return StartCoroutine(LoadSceneInternal(name));
    }

    IEnumerator LoadSceneInternal(string name)
    {           
#if UNITY_EDITOR
        if (name.Contains(".unity"))
        {
            UnityEditor.EditorApplication.LoadLevelInPlayMode(name);
            yield return WaitFor(new SceneLoaded(Path.GetFileNameWithoutExtension(name)));
            yield break;
        }
#endif        
        SceneManager.LoadScene(name);
        yield return WaitFor(new SceneLoaded(name));
    }

    protected Coroutine AssertLabel(string id, string text)
    {
        return StartCoroutine(AssertLabelInternal(id, text));
    }

    protected Coroutine SelectDropdown(string targetName, int value)
    {
        return StartCoroutine(SelectDropdownInternal(targetName, value));
    }

    protected Coroutine SelectDropdown(GameObject o, int value)
    {
        return StartCoroutine(SelectDropdownInternal(o, value));
    }

    IEnumerator SelectDropdownInternal(string targetName, int value)
    {
        var buttonAppeared = new ObjectAppeared(targetName);
        var o = GameObject.Find(targetName);
        yield return SelectDropdown(o, value);
    }

    IEnumerator SelectDropdownInternal(GameObject o, int value)
    {
        o.GetComponent<Dropdown>().value = value;
        yield return null;
    }

    protected Coroutine Press(string buttonName)
    {
        return StartCoroutine(PressInternal(buttonName));
    }

    protected Coroutine Press(GameObject o)
    {
        return StartCoroutine(PressInternal(o));
    }

    IEnumerator WaitForInternal(Condition condition, string stackTrace)
    {
        float time = 0;
        while (!condition.Satisfied())
        {
            if (time > WaitTimeout)
                throw new Exception("Operation timed out: " + condition + "\n" + stackTrace);            
            for (int i = 0; i < WaitIntervalFrames; i++) {
                time += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    IEnumerator PressInternal(string buttonName)
    {
        var buttonAppeared = new ObjectAppeared(buttonName);
        yield return WaitFor(buttonAppeared);
        yield return Press(buttonAppeared.o);
    }

    IEnumerator PressInternal(GameObject o)
    {
        yield return WaitFor(new ButtonAccessible(o));
        Debug.Log("Button pressed: " + o);
        ExecuteEvents.Execute(o, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        yield return null;
    }
                
    IEnumerator AssertLabelInternal(string id, string text)
    {
        yield return WaitFor(new LabelTextAppeared(id, text));
    }
                
    protected abstract class Condition
    {
        protected string param;
        protected string objectName;

        public Condition()
        {
        }

        public Condition(string param)
        {
            this.param = param;
        }

        public Condition(string objectName, string param)
        {
            this.param = param;
            this.objectName = objectName;
        }

        public abstract bool Satisfied();

        public override string ToString()
        {
            return GetType() + " '" + param + "'";
        }
    }

    protected class LabelTextAppeared : Condition
    {
        public LabelTextAppeared(string objectName, string param) : base(objectName, param) {}

        public override bool Satisfied()
        {
            return GetErrorMessage() == null;
        }

        string GetErrorMessage()
        {
            var go = GameObject.Find(objectName);
            if (go == null) return "Label object " + objectName + " does not exist";
            if (!go.activeInHierarchy) return "Label object " + objectName + " is inactive";
            var t = go.GetComponent<Text>();
            if (t == null) return "Label object " + objectName + " has no Text attached";
            if (t.text != param) return "Label " + objectName + "\n text expected: " + param + ",\n actual: " + t.text;
            return null;
        }

        public override string ToString()
        {
            return GetErrorMessage();
        }
    }

    protected class SceneLoaded : Condition
    {        
        public SceneLoaded(string param) : base (param) {}

        public override bool Satisfied()
        {
            return SceneManager.GetActiveScene().name == param;
        }
    }

    protected class ObjectAnimationPlaying : Condition
    {
        public ObjectAnimationPlaying(string objectName, string param) :base (objectName, param) {}

        public override bool Satisfied()
        {
            GameObject gameObject = GameObject.Find(objectName);
            return gameObject.GetComponent<Animation>().IsPlaying(param);
        }
    }

    protected class ObjectAppeared<T> : Condition where T : Component
    {
        public override bool Satisfied()
        {
            var obj = GameObject.FindObjectOfType(typeof (T)) as T;
            return obj != null && obj.gameObject.activeInHierarchy;
        }
    }

    protected class ObjectDisappeared<T> : Condition where T : Component
    {
        public override bool Satisfied()
        {
            var obj = GameObject.FindObjectOfType(typeof(T)) as T;
            return obj == null || !obj.gameObject.activeInHierarchy;
        }
    }

    protected class ObjectAppeared : Condition
    {
        protected string path;
        public GameObject o;

        public ObjectAppeared(string path)
        {
            this.path = path;
        }

        public override bool Satisfied()
        {
            o = GameObject.Find(path);
            return o != null && o.activeInHierarchy;
        }

        public override string ToString()
        {
            return "ObjectAppeared(" + path + ")";
        }
    }

    protected class ObjectDisappeared : ObjectAppeared
    {
        public ObjectDisappeared(string path) : base(path) {}

        public override bool Satisfied()
        {
            return !base.Satisfied();
        }

        public override string ToString()
        {
            return "ObjectDisappeared(" + path + ")";
        }
    }

    protected class BoolCondition : Condition
    {
        private Func<bool> _getter;

        public BoolCondition(Func<bool> getter)
        {
            _getter = getter;
        }

        public override bool Satisfied()
        {
            if (_getter == null) return false;
            return _getter();
        }

        public override string ToString()
        {
            return "BoolCondition(" + _getter + ")";
        }
    }

    protected class ButtonAccessible : Condition
    {
        GameObject button;

        public ButtonAccessible(GameObject button)
        {
            this.button = button;
        }

        public override bool Satisfied()
        {
            return GetAccessibilityMessage() == null;
        }

        public override string ToString()
        {
            return GetAccessibilityMessage() ?? "Button " + button.name + " is accessible";
        }

        string GetAccessibilityMessage()
        {
            if (button == null)
                return "Button " + button + " not found";
            if (button.GetComponent<Button>() == null)
                return "GameObject " + button + " does not have a Button component attached";
            return null;
        }
    }

    protected class DropdownAccessible : Condition
    {
        GameObject _dropdown;

        public DropdownAccessible(GameObject dropdown)
        {
            _dropdown = dropdown;
        }

        public override bool Satisfied()
        {
            return GetAccessibilityMessage() == null;
        }

        public override string ToString()
        {
            return GetAccessibilityMessage() ?? "Button " + _dropdown.name + " is accessible";
        }

        string GetAccessibilityMessage()
        {
            if (_dropdown == null)
                return "Button " + _dropdown + " not found";
            if (_dropdown.GetComponent<Dropdown>() == null)
                return "GameObject " + _dropdown + " does not have a Button component attached";
            return null;
        }
    }
}
