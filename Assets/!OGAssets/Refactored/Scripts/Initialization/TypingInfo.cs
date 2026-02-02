using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using OG.Data;

namespace OG.Initialization
{
  internal readonly struct TypingInfo
  {
    public IReadOnlyList<Type> Types { get; }
    public GetOrderDelegate GetOrder { get; }
    public InitializeAsyncDelegate InitializeAsync { get; }

    public TypingInfo(IEnumerable<Type> types, GetOrderDelegate getOrder, InitializeAsyncDelegate initializeAsync)
    {
      GetOrder = getOrder;
      InitializeAsync = initializeAsync;
      Types = types.ToList();
    }

    internal delegate int GetOrderDelegate(object obj);

    internal delegate UniTask InitializeAsyncDelegate(object obj);
  }
}