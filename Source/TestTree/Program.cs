using NFX;
using NFX.ApplicationModel.Pile;
using NFX.Collections;
using NFX.DataAccess.Distributed;
using SocialTrading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace testTree
{
  public class User
  {
    private User() { }

    public User(Guid id)
    {
      ID = id;
    }


    public Guid ID { get; private set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public DateTime DOB { get; set; }
    public StringMap SocialMsg { get; set; }
    public ulong BuyerScore { get; set; }
    public ulong SellerScore { get; set; }
    public bool IsVendor { get; set; }
    public bool? PayoutApproved { get; set; }
    public bool? ReturnApproved { get; set; }

    public float Age//example business logic
    {
      get { return (float)(App.TimeSource.Now - DOB).TotalDays / 365f; }
    }

  }



  class Program
  {
    static void Main(string[] args)
    {

      TestPP();

    }

    private static void TestPP()
    {
      var m_Pile = new DefaultPile();
      m_Pile.Configure(null);
      // m_Pile.SegmentSize = 512 * 1024 * 1024;
      m_Pile.Start();

      var tree = new Tree<User>(m_Pile, makeUser());
      TreeNode<User> node = null;

      for (int i = 0; i < 1000; i++)
      {
        node = tree.CreateNode(makeUser());
        tree.AddChild(tree.Root, node);
        for (int j = 0; j < 10000; j++)
        {
          tree.AddChild(node, tree.CreateNode(makeUser()));
        }
      }


      //node = null
      foreach (var item in tree.Root.Childs)
      {
        Console.WriteLine(item.Data.Name);
      }
      //tree.RemoveChild(node, node2);
      //tree.GetChilds(tree.Root)
      Console.ReadKey();
      callGS();
      Console.ReadKey();
      tree.Dispose();
      m_Pile.Purge();
    }

    static void callGS()
    {
      var was = GC.GetTotalMemory(false);
      var w = Stopwatch.StartNew();
      GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
      GC.WaitForPendingFinalizers();
      Console.WriteLine("GC Freed {0:n0} bytes in {1:n0} ms".Args(was - GC.GetTotalMemory(true), w.ElapsedMilliseconds));
    }

    public static User makeUser()
    {

      return new User(Guid.NewGuid())
      {
        Name = "Person #" + Guid.NewGuid(),
        Address = "",
        DOB = new DateTime(1980, 1, 1),
        //SocialMsg = sm,
        BuyerScore = 34,
        SellerScore = 473,
        //GVendor = id.Counter % 3 == 0 ? null : (GDID?)id,
        IsVendor = true,
        ReturnApproved = false
      };
    }
  }


}
