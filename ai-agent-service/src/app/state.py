from typing import TypedDict, List, Dict


class AgentState(TypedDict, total=False):
    goal: str                 # 用户目标
    plan: List[str]           # manager 生成的执行计划（Agent 名队列）
    next_agent: str           # manager 指派的下一个 Agent
    current_agent: str        # 最近完成工作的 Agent
    agent_outputs: Dict[str, str]  # {"research": ..., "write": ..., "review": ...}
    review_score: int         # reviewer 给出的评分，manager 据此决定是否退回
    messages: List[dict]      # 消息流（团队"会议记录"）
    answer: str               # 最终交付结果
    round: int                # 已执行的 manager 轮次
    max_rounds: int           # 终止保护（防死循环）
    done: bool                # 是否完成