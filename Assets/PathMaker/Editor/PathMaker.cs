using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PathMaker
{
    public class PathMaker : EditorWindow
    {
        public enum DrawType
        {
            Free,
            Linear,
            Curve,
        }

        public enum AnimationY
        {
            Y,
            Z,
        }

        public static PathMaker Instance { get; set; }
        private DrawType drawType = DrawType.Free;

        private PathData pathData = new PathData();
        private Rect baseRect;
        private Rect drawRect;
        private int nearestPathIdx = -1;    // 마우스와 가장 가까운 점 idx
        private int startDrawingIdx = -1;   // 이어그리기 시 시작점 idx
        private int movingPathIdx = -1;     // 옮기는 중인 점 idx
        private bool mouseDown_0;
        private bool mouseDown_1;
        private float minDistance = 10f;
        private float speed = 1f;
        private AnimationY animationY = AnimationY.Y;
        private AnimationClip animationClip;
        private string loadedJSON;

        private const int startX = 200;
        private const int startY = 100;
        private const int mapWidth = 250;
        private const int optionPosX = (startX * 2) + mapWidth + 100;
        private const int optionWidth = 250;

        private const int textWidth = 57;
        private const int textHeight = 16;

        [MenuItem("Window/Path Maker")]
        public static void ShowWindow()
        {
            Instance = GetWindow<PathMaker>();
            Command.Instance.Clear();
        }

        private void OnGUI()
        {
            if (Instance == null)
            {
                Instance = GetWindow<PathMaker>();
                Command.Instance.Clear();
            }

            using (new EditorGUI.DisabledGroupScope(pathData.Data.Count > 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    pathData.Left = EditorGUILayout.FloatField("Left", pathData.Left);
                    pathData.Right = EditorGUILayout.FloatField("Right", pathData.Right);

                    if (pathData.Right < pathData.Left)
                        pathData.Right = pathData.Left;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    pathData.Top = EditorGUILayout.FloatField("Top", pathData.Top);
                    pathData.Bottom = EditorGUILayout.FloatField("Bottom", pathData.Bottom);

                    if (pathData.Bottom > pathData.Top)
                        pathData.Bottom = pathData.Top;
                }
            }

            float width = pathData.Right - pathData.Left;
            float height = pathData.Top - pathData.Bottom;
            float ratio = height / width;

            baseRect = new Rect(startX, startY, mapWidth, mapWidth * ratio);
            drawRect = new Rect(2, 44, baseRect.xMin + baseRect.xMax - 2, baseRect.yMin + baseRect.yMax - 44);
            EditorGUI.DrawRect(drawRect, Color.white * 0.4f);
            EditorGUI.DrawRect(baseRect, Color.gray);

            EditorGUI.LabelField(CreateRect(baseRect.x, baseRect.y, false, true), pathData.Left + ", " + pathData.Top);
            EditorGUI.LabelField(CreateRect(baseRect.x + baseRect.size.x, baseRect.y + baseRect.size.y, true, false), pathData.Right + ", " + pathData.Bottom);

            // Draw Options
            using (new EditorGUI.DisabledGroupScope(!Command.Instance.CanUndo))
            {
                if (GUI.Button(CreateRect(optionPosX, startY - 40, true, true, optionWidth), "UnDo Draw"))
                {
                    Command.Instance.Undo(ref pathData);
                }
            }
            using (new EditorGUI.DisabledGroupScope(!Command.Instance.CanRedo))
            {
                if (GUI.Button(CreateRect(optionPosX, startY - 20, true, true, optionWidth), "ReDo Draw"))
                {
                    Command.Instance.Redo(ref pathData);
                }
            }

            Event e = Event.current;
            EditorGUI.LabelField(CreateRect(optionPosX, startY, true, true, optionWidth), e.mousePosition.ToString());
            drawType = (DrawType)EditorGUI.EnumPopup(CreateRect(optionPosX, startY + 20, true, true, optionWidth), drawType);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.LabelField(CreateRect(optionPosX, startY + 40, true, true, 50), "Offset");
                pathData.Offset = EditorGUI.Vector2Field(CreateRect(optionPosX + 50, startY + 40, true, true, optionWidth - 50), "", pathData.Offset);
            }
            using (new EditorGUI.DisabledGroupScope(drawType != DrawType.Free))
            {
                EditorGUI.LabelField(CreateRect(optionPosX, startY + 60, true, true, optionWidth), "Min Distance between points");
                minDistance = EditorGUI.Slider(CreateRect(optionPosX, startY + 80, true, true, optionWidth), "", minDistance, 1f, 50f);
            }
            animationClip = EditorGUI.ObjectField(CreateRect(optionPosX, startY + 160, true, true, optionWidth), animationClip, typeof(AnimationClip), false) as AnimationClip;
            speed = EditorGUI.FloatField(CreateRect(optionPosX, startY + 180, true, true, optionWidth), "Speed", speed);
            speed = Mathf.Max(0.01f, speed);
            animationY = (AnimationY)EditorGUI.EnumPopup(CreateRect(optionPosX, startY + 200, true, true, optionWidth), "Animation Y", animationY);
            if (GUI.Button(CreateRect(optionPosX, startY + 220, true, true, optionWidth, 18), "Save To Animation Clip"))
            {
                SaveToAnimation(animationClip);
            }

            EditorGUI.LabelField(CreateRect(optionPosX, startY + 280, true, true, optionWidth), "JSON String");
            if (GUI.Button(CreateRect(optionPosX, startY + 300, true, true, optionWidth), "Copy to Clipboard"))
            {
                TextEditor te = new TextEditor();
                te.text = PathToJSON();
                te.SelectAll();
                te.Copy();
            }
            loadedJSON = EditorGUI.TextField(CreateRect(optionPosX, startY + 340, true, true, optionWidth), loadedJSON);
            if (GUI.Button(CreateRect(optionPosX, startY + 360, true, true, optionWidth), "Load from JSON"))
            {
                JSONTOPath(loadedJSON);
            }

            if (GUI.Button(CreateRect(optionPosX, startY + 440, true, true, optionWidth, 18), "Clear All Path"))
            {
                Command.Instance.RegisterUndo(pathData);

                pathData.Data.Clear();
                nearestPathIdx = -1;
                startDrawingIdx = -1;
                movingPathIdx = -1;
            }
            
            switch (drawType)
            {
                case DrawType.Free:
                    DrawFree();
                    break;
                case DrawType.Linear:
                    DrawLinear();
                    break;
                case DrawType.Curve:
                    DrawCurve();
                    break;
            }

            DrawLines(5f);

            // 마우스로부터 가까운 점 스냅
            FindNeareatPath();

            if (IsValidPosition(e.mousePosition))
            {
                EditorGUI.LabelField(CreateRect(e.mousePosition.x, e.mousePosition.y, false, true, 70f), ScreenToWorld(e.mousePosition).ToString());
            }
        }

        private Rect CreateRect(float x, float y, bool right, bool up, float width = textWidth, float height = textHeight)
        {
            Rect rect = new Rect(x, y, width, height);
            if (right)
                rect.x -= textWidth;
            if (up)
                rect.y -= textHeight;

            return rect;
        }

        private void Update()
        {
            if (EditorWindow.focusedWindow == this && EditorWindow.mouseOverWindow == this)
            {
                Repaint();
            }
        }

        private void DrawFree()
        {
            Event e = Event.current;
            if (e.button == 0)
            {
                if (e.type == EventType.MouseDown)
                {
                    if (IsValidPosition(e.mousePosition))
                    {
                        // 가까운 점부터 이어서 시작
                        if (nearestPathIdx >= 0 && !pathData.Data[nearestPathIdx].IsAnchor)
                        {
                            Command.Instance.RegisterUndo(pathData);

                            pathData.Data.RemoveRange(nearestPathIdx, pathData.Data.Count - nearestPathIdx);
                            startDrawingIdx = nearestPathIdx;
                            pathData.Data.Add(new Path(e.mousePosition));
                            mouseDown_0 = true;
                        }
                        else if (pathData.Data.Count == 0)
                        {
                            Command.Instance.RegisterUndo(pathData);

                            startDrawingIdx = 0;
                            pathData.Data.Add(new Path(e.mousePosition));
                            mouseDown_0 = true;
                        }
                    }
                }
                else if (e.type == EventType.MouseDrag && mouseDown_0)
                {
                    if (IsValidPosition(e.mousePosition))
                        pathData.Data.Add(new Path(e.mousePosition));
                }
                else if (e.type == EventType.MouseUp && mouseDown_0)
                {
                    if (pathData.Data.Count < 2)
                    {
                        pathData.Data.Clear();
                    }
                    else
                    {
                        List<Path> addList = new List<Path>();
                        addList.Add(pathData.Data[startDrawingIdx]);
                        for (int i = startDrawingIdx; i < pathData.Data.Count - 2;)
                        {
                            int k = i + 1;
                            while (k < pathData.Data.Count - 1)
                            {
                                float distance = (pathData.Data[i].Pos - pathData.Data[k++].Pos).sqrMagnitude;
                                if (distance > minDistance * minDistance)
                                    break;
                            }

                            i = k;
                            addList.Add(pathData.Data[i]);
                        }

                        pathData.Data.RemoveRange(startDrawingIdx, pathData.Data.Count - startDrawingIdx);
                        pathData.Data.AddRange(addList);
                    }

                    mouseDown_0 = false;
                    nearestPathIdx = -1;
                    startDrawingIdx = -1;
                }
            }
            else if (e.button == 1)
            {
                if (e.type == EventType.MouseDown)
                {
                    if (nearestPathIdx >= 0)
                    {
                        Command.Instance.RegisterUndo(pathData);

                        movingPathIdx = nearestPathIdx;
                        pathData.Data[movingPathIdx].Pos = e.mousePosition;
                        mouseDown_1 = true;
                    }
                }
                else if (e.type == EventType.MouseDrag && mouseDown_1)
                {
                    if (IsValidPosition(e.mousePosition))
                        pathData.Data[movingPathIdx].Pos = e.mousePosition;
                }
                else if (e.type == EventType.MouseUp && mouseDown_1)
                {
                    mouseDown_1 = false;
                    movingPathIdx = -1;
                }
            }
        }

        private void DrawLinear()
        {
            Event e = Event.current;
            if (e.button == 0)
            {
                if (e.type == EventType.MouseDown)
                {
                    if (IsValidPosition(e.mousePosition))
                    {
                        // 가까운 점부터 이어서 시작
                        if (nearestPathIdx >= 0 && !pathData.Data[nearestPathIdx].IsAnchor)
                        {
                            Command.Instance.RegisterUndo(pathData);

                            Vector3 cachedPos = pathData.Data[nearestPathIdx].Pos;
                            pathData.Data.RemoveRange(nearestPathIdx, pathData.Data.Count - nearestPathIdx);
                            startDrawingIdx = nearestPathIdx;
                            pathData.Data.Add(new Path(cachedPos));
                            mouseDown_0 = true;
                        }
                        else if (pathData.Data.Count == 0)
                        {
                            Command.Instance.RegisterUndo(pathData);

                            startDrawingIdx = 0;
                            pathData.Data.Add(new Path(e.mousePosition));
                            mouseDown_0 = true;
                        }
                    }
                }
                else if (e.type == EventType.MouseDrag && mouseDown_0)
                {
                    if (IsValidPosition(e.mousePosition))
                    {
                        if (startDrawingIdx >= 0 && startDrawingIdx < pathData.Data.Count)
                        {
                            Vector3 cachedPos = pathData.Data[startDrawingIdx].Pos;
                            pathData.Data.RemoveRange(startDrawingIdx, pathData.Data.Count - startDrawingIdx);

                            pathData.Data.Add(new Path(cachedPos));
                            pathData.Data.Add(new Path(e.mousePosition));
                        }
                    }
                }
                else if (e.type == EventType.MouseUp && mouseDown_0)
                {
                    if (pathData.Data.Count < 2)
                    {
                        pathData.Data.Clear();
                    }

                    mouseDown_0 = false;
                    nearestPathIdx = -1;
                    startDrawingIdx = -1;
                }
            }
            else if (e.button == 1)
            {
                if (e.type == EventType.MouseDown)
                {
                    if (nearestPathIdx >= 0)
                    {
                        Command.Instance.RegisterUndo(pathData);

                        movingPathIdx = nearestPathIdx;
                        pathData.Data[movingPathIdx].Pos = e.mousePosition;
                        mouseDown_1 = true;
                    }
                }
                else if (e.type == EventType.MouseDrag && mouseDown_1)
                {
                    if (IsValidPosition(e.mousePosition))
                        pathData.Data[movingPathIdx].Pos = e.mousePosition;
                }
                else if (e.type == EventType.MouseUp && mouseDown_1)
                {
                    mouseDown_1 = false;
                    movingPathIdx = -1;
                }
            }
        }

        private void DrawCurve()
        {
            Event e = Event.current;
            if (e.button == 0)
            {
                if (e.type == EventType.MouseDown)
                {
                    if (IsValidPosition(e.mousePosition))
                    {
                        // 가까운 점부터 이어서 시작
                        if (nearestPathIdx >= 0 && !pathData.Data[nearestPathIdx].IsAnchor)
                        {
                            Command.Instance.RegisterUndo(pathData);

                            Vector3 cachedPos = pathData.Data[nearestPathIdx].Pos;
                            pathData.Data.RemoveRange(nearestPathIdx, pathData.Data.Count - nearestPathIdx);
                            startDrawingIdx = nearestPathIdx;
                            pathData.Data.Add(new Path(cachedPos));
                            mouseDown_0 = true;
                        }
                        else if (pathData.Data.Count == 0)
                        {
                            Command.Instance.RegisterUndo(pathData);

                            startDrawingIdx = 0;
                            pathData.Data.Add(new Path(e.mousePosition));
                            mouseDown_0 = true;
                        }
                    }
                }
                else if (e.type == EventType.MouseDrag)
                {
                    if (IsValidPosition(e.mousePosition) && mouseDown_0)
                    {
                        if (startDrawingIdx >= 0 && startDrawingIdx < pathData.Data.Count)
                        {
                            Vector3 cachedPos = pathData.Data[startDrawingIdx].Pos;
                            pathData.Data.RemoveRange(startDrawingIdx, pathData.Data.Count - startDrawingIdx);

                            pathData.Data.Add(new Path(cachedPos));
                            pathData.Data.Add(new Path(Vector2.Lerp(cachedPos, e.mousePosition, 0.5f), true));
                            pathData.Data.Add(new Path(e.mousePosition));
                        }
                    }
                }
                else if (e.type == EventType.MouseUp && mouseDown_0)
                {
                    if (pathData.Data.Count < 2)
                    {
                        pathData.Data.Clear();
                    }

                    mouseDown_0 = false;
                    nearestPathIdx = -1;
                    startDrawingIdx = -1;
                }
            }
            else if (e.button == 1)
            {
                if (e.type == EventType.MouseDown)
                {
                    if (nearestPathIdx >= 0)
                    {
                        Command.Instance.RegisterUndo(pathData);

                        movingPathIdx = nearestPathIdx;
                        pathData.Data[movingPathIdx].Pos = e.mousePosition;
                        mouseDown_1 = true;
                    }
                }
                else if (e.type == EventType.MouseDrag && mouseDown_1)
                {
                    if (IsValidPosition(e.mousePosition))
                        pathData.Data[movingPathIdx].Pos = e.mousePosition;
                }
                else if (e.type == EventType.MouseUp && mouseDown_1)
                {
                    mouseDown_1 = false;
                    movingPathIdx = -1;
                }
            }
        }

        private void DrawLines(float width)
        {
            if (pathData.Data.Count <= 1)
                return;

            Color prevColor = Handles.color;
            Handles.color = Color.green;

            Handles.BeginGUI();

            for (int i = 1; i < pathData.Data.Count; ++i)
            {
                Path prevPath = pathData.Data[i - 1];
                if (prevPath.IsAnchor)
                    continue;

                Path curPath = pathData.Data[i];

                if (curPath.IsAnchor)
                {
                    // 곡선을 위한 앵커인 경우 곡선 표시
                    Path nextPath = pathData.Data[i + 1];
                    List<Vector2> curveList = GetCurveList(prevPath, curPath, nextPath);
                    for (int k = 1; k < curveList.Count; ++k)
                    {
                        Vector2 curveStart = curveList[k - 1];
                        Vector2 curveEnd = curveList[k];
                        Handles.DrawAAPolyLine(width, curveStart, curveEnd);

                        if (pathData.Offset.sqrMagnitude > 0)
                        {
                            Vector2 worldStart = ScreenToWorld(curveStart) + pathData.Offset;
                            Vector2 worldEnd = ScreenToWorld(curveEnd) + pathData.Offset;
                            DrawLine(WorldToScreen(worldStart), WorldToScreen(worldEnd), width, Color.cyan);
                        }
                    }

                    DrawCircle(curPath.Pos, 5f, Color.red);
                    DrawLine(prevPath.Pos, curPath.Pos, 1f, Color.red);
                    DrawLine(curPath.Pos, nextPath.Pos, 1f, Color.red);
                }
                else
                {
                    Vector2 start = prevPath.Pos;
                    Vector2 end = curPath.Pos;
                    Handles.DrawAAPolyLine(width, start, end);

                    if (pathData.Offset.sqrMagnitude > 0)
                    {
                        Vector2 worldStart = ScreenToWorld(start) + pathData.Offset;
                        Vector2 worldEnd = ScreenToWorld(end) + pathData.Offset;
                        DrawLine(WorldToScreen(worldStart), WorldToScreen(worldEnd), width, Color.cyan);
                    }
                }
            }
            Handles.CircleHandleCap(0, pathData.Data[pathData.Data.Count - 1].Pos, Quaternion.identity, width, EventType.Repaint);
            Handles.EndGUI();

            Handles.color = prevColor;
        }

        private void DrawCircle(Vector2 pos, float radius, Color color)
        {
            Color prevColor = Handles.color;
            Handles.color = color;

            Handles.BeginGUI();
            Handles.CircleHandleCap(0, pos, Quaternion.identity, radius, EventType.Repaint);
            Handles.EndGUI();

            Handles.color = prevColor;
        }

        private void DrawLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Color prevColor = Handles.color;
            Handles.color = color;

            Handles.BeginGUI();
            Handles.DrawAAPolyLine(width, start, end);
            Handles.EndGUI();

            Handles.color = prevColor;
        }

        private bool IsValidPosition(Vector2 pos)
        {
            return drawRect.Contains(pos);
        }

        public Vector2 ScreenToWorld(Vector2 pos)
        {
            float x = (pos.x - baseRect.xMin) / (baseRect.xMax - baseRect.xMin);
            float y = (pos.y - baseRect.yMin) / (baseRect.yMax - baseRect.yMin);

            return new Vector2(Mathf.LerpUnclamped(pathData.Left, pathData.Right, x), Mathf.LerpUnclamped(pathData.Top, pathData.Bottom, y));
        }

        public Vector2 WorldToScreen(Vector2 pos)
        {
            float x = (pos.x - pathData.Left) / (pathData.Right - pathData.Left);
            float y = (pos.y - pathData.Top) / (pathData.Bottom - pathData.Top);

            return new Vector2(Mathf.LerpUnclamped(baseRect.xMin, baseRect.xMax, x), Mathf.LerpUnclamped(baseRect.yMin, baseRect.yMax, y));
        }

        private void FindNeareatPath()
        {
            if (pathData.Data.Count > 0)
            {
                Event e = Event.current;

                nearestPathIdx = 0;
                float min = (pathData.Data[0].Pos - e.mousePosition).sqrMagnitude;

                for (int i = 1; i < pathData.Data.Count; ++i)
                {
                    float distance = (pathData.Data[i].Pos - e.mousePosition).sqrMagnitude;
                    if (distance < min)
                    {
                        min = distance;
                        nearestPathIdx = i;
                    }
                }

                if (min <= 49f)
                    DrawCircle(pathData.Data[nearestPathIdx].Pos, 5f, Color.yellow);
                else
                    nearestPathIdx = -1;
            }
            else
            {
                nearestPathIdx = -1;
            }
        }

        private List<Vector2> GetCurveList(Path start, Path anchor, Path end)
        {
            List<Vector2> curveList = new List<Vector2>();
            for (float t = 0; t < 1f; t += 0.1f)
            {
                Vector3 curvePos = (((1 - t) * (1 - t)) * start.Pos) + (2 * t * (1 - t) * anchor.Pos) + ((t * t) * end.Pos);

                if (IsValidPosition(curvePos))
                    curveList.Add(curvePos);
            }
            curveList.Add(end.Pos);

            return curveList;
        }

        private void SaveToAnimation(AnimationClip clip)
        {
            if (pathData.Data.Count < 2)
                return;

            float t = 0f;
            AnimationCurve xCurve = new AnimationCurve();
            AnimationCurve yCurve = new AnimationCurve();

            for (int i = 0; i < pathData.Data.Count; ++i)
            {
                Path prevPath = (i > 0) ? pathData.Data[i - 1] : null;
                if (prevPath != null && prevPath.IsAnchor)
                    continue;
                Path curPath = pathData.Data[i];

                if (curPath.IsAnchor)
                {
                    Path nextPath = pathData.Data[i + 1];
                    List<Vector2> curveList = GetCurveList(prevPath, curPath, nextPath);
                    for (int k = 1; k < curveList.Count; ++k)
                    {
                        t += CalculateTime(curveList[k], curveList[k - 1]);

                        Vector2 worldPos = ScreenToWorld(curveList[k]) + pathData.Offset;
                        AddKeyFrame(xCurve, t, worldPos.x);
                        AddKeyFrame(yCurve, t, worldPos.y);
                    }
                }
                else
                {
                    if (prevPath != null)
                        t += CalculateTime(prevPath.Pos, curPath.Pos);

                    Vector2 worldPos = ScreenToWorld(pathData.Data[i].Pos) + pathData.Offset;
                    AddKeyFrame(xCurve, t, worldPos.x);
                    AddKeyFrame(yCurve, t, worldPos.y);
                }
            }

            clip.ClearCurves();
            clip.SetCurve("", typeof(Transform), "localPosition.x", xCurve);
            if (animationY == AnimationY.Y)
                clip.SetCurve("", typeof(Transform), "localPosition.y", yCurve);
            else if (animationY == AnimationY.Z)
                clip.SetCurve("", typeof(Transform), "localPosition.z", yCurve);
        }

        private void AddKeyFrame(AnimationCurve curve, float t, float v)
        {
            curve.AddKey(t, v);
            AnimationUtility.SetKeyLeftTangentMode(curve, curve.keys.Length - 1, AnimationUtility.TangentMode.Linear);
        }

        private float CalculateDistance()
        {
            float distance = 0;
            for (int i = 1; i < pathData.Data.Count; ++i)
            {
                Path prevPath = pathData.Data[i - 1];
                if (prevPath.IsAnchor)
                    continue;

                Path curPath = pathData.Data[i];

                if (curPath.IsAnchor)
                {
                    Path nextPath = pathData.Data[i + 1];
                    List<Vector2> curveList = GetCurveList(prevPath, curPath, nextPath);
                    for (int k = 1; k < curveList.Count; ++k)
                    {
                        distance += CalculateDistance(curveList[k], curveList[k - 1]);
                    }
                }
                else
                {
                    distance += CalculateDistance(curPath.Pos, prevPath.Pos);
                }
            }

            return distance;
        }

        private float CalculateDistance(Vector2 start, Vector2 end)
        {
            start = ScreenToWorld(start);
            end = ScreenToWorld(end);

            return (end - start).magnitude;
        }

        private float CalculateTime(Vector2 start, Vector2 end)
        {
            float distance = CalculateDistance(start, end);

            return distance / speed;
        }

        private string PathToJSON()
        {
            return JsonUtility.ToJson(pathData);
        }

        private void JSONTOPath(string json)
        {
            Command.Instance.RegisterUndo(pathData);

            pathData = JsonUtility.FromJson<PathData>(json);
        }
    }
}
