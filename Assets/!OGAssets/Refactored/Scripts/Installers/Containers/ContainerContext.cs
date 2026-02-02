using System.Collections.Generic;
using OG.Data;
using UnityEngine;
using Zenject;

namespace OG.Installers
{
  public abstract class ContainerContext : MonoBehaviour
  {
    public DiContainer Container { get; private set; }

    public abstract AppContextType Context { get; }

    [Inject]
    private void OnInject(DiContainer container)
    {
      Container = container;
      Register(this);
    }

    protected virtual void Register(ContainerContext containerContext)
      => Container.Resolve<ProjectContainer>().Register(this);

    protected virtual void Unregister(ContainerContext containerContext)
      => Container.Resolve<ProjectContainer>().Unregister(this);

    private void OnDestroy()
      => Unregister(this);
  }
}