using System.Collections.Generic;

namespace PathMaker
{
    public class Command
    {
        private static Command instance;
        public static Command Instance
        {
            get
            {
                if (instance == null)
                    instance = new Command();

                return instance;
            }
        }

        private Stack<PathData> undoStack = new Stack<PathData>();
        public bool CanUndo { get { return undoStack.Count > 0; } }

        private Stack<PathData> redoStack = new Stack<PathData>();
        public bool CanRedo { get { return redoStack.Count > 0; } }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
        }

        public void RegisterUndo(PathData data)
        {
            undoStack.Push(data.Clone() as PathData);
        }

        public void Undo(ref PathData data)
        {
            if (CanUndo)
            {
                redoStack.Push(data.Clone() as PathData);
                data = undoStack.Pop();
            }
        }

        public void Redo(ref PathData data)
        {
            if (CanRedo)
            {
                undoStack.Push(data.Clone() as PathData);
                data = redoStack.Pop();
            }
        }
    }
}
