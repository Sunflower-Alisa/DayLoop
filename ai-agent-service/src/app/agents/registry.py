_registry: dict[str, object] = {}


def register(name: str):
    def decorator(cls):
        _registry[name] = cls
        return cls
    return decorator


def get_agent(name: str) -> object:
    return _registry[name]


def list_agents() -> list[str]:
    return list(_registry)

