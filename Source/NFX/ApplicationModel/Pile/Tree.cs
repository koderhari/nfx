using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NFX.ApplicationModel.Pile
{
  public class Tree<T> : DisposableObject
  {
    //private object _syncObject = new object();
    public NFX.OS.ManyReadersOneWriterSynchronizer RWSynchronizer;
    private IPile _pile;
    private Dictionary<PilePointer, Dictionary<PilePointer, PilePointer>> _parentChilds = new Dictionary<PilePointer, Dictionary<PilePointer, PilePointer>>();
    //need store nodes without parent
    private PilePointer _root;

    public Tree(IPile pile, T rootData)
    {
      _pile = pile ?? throw new ArgumentNullException(nameof(pile));
      CreateRoot(rootData);
    }

    public TreeNode<T> Root { get; private set; }

    private void CreateRoot(T rootData)
    {
      Root = new TreeNode<T>(rootData);
      _root = _pile.Put(Root);
      _parentChilds.Add(_root, new Dictionary<PilePointer, PilePointer>());
      Root.PilePointer = _root;
      Root.Tree = this;
    }

    public TreeNode<T> CreateNode(T data)
    {
      var node = new TreeNode<T>(data);
      var pilePonter = _pile.Put(node);
      node.Tree = this;
      _parentChilds.Add(pilePonter, new Dictionary<PilePointer, PilePointer>());
      return node;
    }

    public bool AddChild(TreeNode<T> parent, TreeNode<T> child)
    {
      if (!CheckIfNodeValid(parent, silent: true)) return false;
      if (!CheckIfNodeValid(child, silent: true)) return false;
      if (!getWriteLock()) return false;

      try
      {
        if (!CheckIfNodeValid(parent, silent: true)) return false;
        if (!CheckIfNodeValid(child, silent: true)) return false;
        var oldParent = FindParent(child.PilePointer);
        if (oldParent.HasValue)
        {
          _parentChilds[oldParent.Value].Remove(child.PilePointer);
        }

        _parentChilds[parent.PilePointer].Add(child.PilePointer, child.PilePointer);
        return true;
      }
      finally
      {
        releaseWriteLock();
      }
    }

    private PilePointer? FindParent(PilePointer pointer)
    {
      foreach (var parentNode in _parentChilds)
      {
        if (parentNode.Value.ContainsKey(pointer))
        {
          return parentNode.Key;
        }
      }

      return null;
    }

    public IEnumerable<TreeNode<T>> GetChilds(TreeNode<T> parent)
    {
      if (!CheckIfNodeValid(parent, silent: true)) return new List<TreeNode<T>>();
      if (!getReadLock()) return null;

      try
      {
        if (!CheckIfNodeValid(parent, silent: true)) return new List<TreeNode<T>>();
        var result = new List<TreeNode<T>>();
        foreach (var childPP in _parentChilds[parent.PilePointer].Keys)
        {
          var child = (TreeNode<T>)_pile.Get(childPP);
          child.PilePointer = childPP;
          child.Tree = this;
          result.Add(child);
        }
        return result;
      }
      finally
      {
        releaseReadLock();
      }
    }

    public bool RemoveChild(TreeNode<T> parent, TreeNode<T> child)
    {
      if (CheckIfNodeValid(parent, silent: true)) return false;
      if (CheckIfNodeValid(child, silent: true)) return false;
      if (!_parentChilds[parent.PilePointer].ContainsKey(child.PilePointer)) return false;
      if (!getWriteLock()) return false;

      try
      {
        if (CheckIfNodeValid(parent, silent: true)) return false;
        if (CheckIfNodeValid(child, silent: true)) return false;
        if (!_parentChilds[parent.PilePointer].ContainsKey(child.PilePointer)) return false;

        var childPP = _parentChilds[parent.PilePointer][child.PilePointer];
        _parentChilds[parent.PilePointer].Remove(childPP);
        return true;
      }
      finally
      {
        releaseWriteLock();
      }
    }


    public bool RemoveNode(TreeNode<T> child)
    {
      if (CheckIfNodeValid(child, silent: true)) return false;
      if (!getWriteLock()) return false;
      try
      {
        if (CheckIfNodeValid(child, silent: true)) return false;
        var parent = FindParent(child.PilePointer);
        if (parent.HasValue)
        {
          _parentChilds[parent.Value].Remove(child.PilePointer);
        }

        _pile.Delete(child.PilePointer);
        _parentChilds.Remove(child.PilePointer);
        child.Tree = null;
        child.PilePointer = new PilePointer();
        return true;
      }
      finally
      {
        releaseWriteLock();
      }
    }

    public bool UpdateValue(TreeNode<T> node)
    {
      if (CheckIfNodeValid(node, silent: true)) return false;
      if (!getWriteLock()) return false;
      try
      {
        if (CheckIfNodeValid(node, silent: true)) return false;
        var newPilePointer = _pile.Put(node.PilePointer, node);
        return true;
      }
      finally
      {
        releaseWriteLock();
      }
    }

    bool CheckIfNodeValid(TreeNode<T> node, bool silent = false)
    {
      if (!node.PilePointer.Valid)
      {
        if (silent) return false;
        throw new InvalidOperationException("Node not attached to tree");
      }

      if (!_parentChilds.ContainsKey(node.PilePointer))
      {
        if (silent) return false;
        throw new InvalidOperationException("Node not attached to tree");
      }

      return true;
    }

    private bool getReadLock()
    {
      return RWSynchronizer.GetReadLock((_) => this.Disposed);
    }

    private void releaseReadLock()
    {
      RWSynchronizer.ReleaseReadLock();
    }

    private bool getWriteLock()
    {
      return RWSynchronizer.GetWriteLock((_) => this.Disposed);
    }

    private void releaseWriteLock()
    {
      RWSynchronizer.ReleaseWriteLock();
    }

    protected override void Destructor()
    {
      base.Destructor();

      foreach (var pointer in _parentChilds.Keys)
      {
        _pile.Delete(pointer);
      }
      _parentChilds.Clear();
    }
  }

  //T возможно стоит наследовать некиий Notifier т.е. когда свойство меняется посылать событие

  public class TreeNode<T>
  {
    internal Tree<T> Tree { get; set; }

    internal PilePointer PilePointer { get; set; }

    public T Data { get; private set; }

    internal TreeNode(T data)
    {
      Data = data;
    }

    public IEnumerable<TreeNode<T>> Childs
    {
      get
      {
        if (Tree == null) return new List<TreeNode<T>>();
        return Tree.GetChilds(this);
      }
    }

    public bool UpdateData()
    {
      if (Tree == null) return false;
      return Tree.UpdateValue(this);
    }

    public bool AddChild(TreeNode<T> child)
    {
      if (Tree == null) return false;
      return Tree.AddChild(this, child);
    }

    public bool RemoveChild(TreeNode<T> child)
    {
      if (Tree == null) return false;
      return Tree.RemoveChild(this, child);
    }

  }

}
