from app.agents.registry import list_agents


def build_graph():
    raise NotImplementedError("LangGraph 编排待实现，注册的 agents: " + ",".join(list_agents()))
