using System;
using System.Reflection;
using UnityEngine;

namespace BatchBuild
{
    public class OverrideBuildMethod
    {
        public string typeName;
        public string methodName;
        public string assemblyName;

        public Type myType;
        public MethodInfo methodInfo;

        public string FullyTypeName => typeName + ", " + assemblyName;

        public static bool TryParse(string arg, out OverrideBuildMethod parsed)
        {
            string typeName;
            string methodName;
            string assemblyName;

            string[] splits = arg.Split(',');

            if (splits.Length != 1 && splits.Length != 2)
            {
                Debug.LogError($"[型名.メソッド名, アセンブリ名] または [型名.メソッド名] の形式で指定する必要があります: [{arg}]");
                parsed = null;
                return false;
            }

            // アセンブリ名を解析
            if (splits.Length == 2)
            {
                assemblyName = splits[1].Trim();
            }
            else
            {
                // 指定なしの場合は "Assembly-CSharp" を使う
                assemblyName = "Assembly-CSharp";
            }

            arg = splits[0].Trim();
            int lastIndex = arg.LastIndexOf('.');

            if (lastIndex == -1)
            {
                Debug.LogError("メソッド呼び出しには TypeName. を頭につける必要があります: " + arg);
                parsed = null;
                return false;
            }

            typeName = arg.Substring(0, lastIndex);
            methodName = arg.Substring(lastIndex + 1, arg.Length - (lastIndex + 1));

            parsed = new OverrideBuildMethod()
            {
                methodName = methodName,
                typeName = typeName,
                assemblyName = assemblyName,
            };

            return true;
        }

        public bool TryGetType()
        {
            try
            {
                // Get the type of a specified class.
                myType = Type.GetType(FullyTypeName);

                if (myType == null)
                {
                    Debug.LogError($"Not found type: [{FullyTypeName}]");
                    return false;
                }

                methodInfo = myType.GetMethod(methodName);

                if (methodInfo == null)
                {
                    Debug.LogError("Not found methodInfo: " + methodName);
                    return false;
                }
                
                return true;
            }
            catch (TypeLoadException e)
            {
                Debug.LogError(e);
            }

            return false;
        }
    }
}