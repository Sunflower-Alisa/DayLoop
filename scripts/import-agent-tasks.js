const db = require('../backend/src/database');

const USER_ID = 1; // alisa

const START = '10:00';
const END = '19:30';
const CATEGORY = 'AI Agent';
const PRIORITY = 1;

const days = [
  { offset: 0,  day: 'Day25', title: 'Agent Service API化', note: '阶段: Agent API | 核心目标: Agent服务化。把已有Agent从Python Script改造成FastAPI服务: GET /api/v1/health, POST /api/v1/chat; 复用已有LLM Wrapper。验收: DayLoop尚未接入，但独立调用Agent API已成功。' },
  { offset: 1,  day: 'Day26', title: 'DayLoop集成', note: '阶段: DayLoop集成 | 核心目标: DayLoop ↔ Agent。选择主后端(.NET或Node)增加AI API Client，创建AI聊天页面，支持user_id/session_id/conversation_id。验收(Milestone 1): 打开DayLoop → 聊天窗口 → 输入问题 → Agent返回答案。' },
  { offset: 2,  day: 'Day27', title: 'Context Engineering', note: '阶段: Context | 核心目标: 用户上下文。在Context中加入User Profile/Resume/Skills/Target Position/Job Preference/Conversation History，整理为build_context()。测试: "我适合这个岗位吗?"应结合技能、目标岗位、简历回答。' },
  { offset: 3,  day: 'Day28', title: 'Tools + DayLoop API', note: '阶段: Tools | 核心目标: Agent访问DayLoop数据。实现第一批Tools: get_user_profile/get_resume/get_skills/get_job_preferences/update_resume/update_skills，通过DayLoop API访问数据库。重点测试Tool选择、参数、API成功、异常处理。' },
  { offset: 4,  day: 'Day29', title: 'RAG知识库', note: '阶段: RAG | 核心目标: 知识库接入。接入Day11~17的RAG, 只放AI Agent/RAG/Memory/LangGraph/Agent Interview及个人笔记。流程: Document→Loader→Chunk→Embedding→Chroma→Retriever→Agent Tool knowledge_search(query)。准备20个固定问题测Top1/3/5。' },
  { offset: 5,  day: 'Day30', title: 'Memory长期记忆', note: '阶段: Memory | 核心目标: 长期记忆。Memory分类: User Preference/Career Goal/Skills/Experience/Important Facts。流程: Conversation→Extraction→Store→Retrieval→Context→LLM。测试记忆冲突: 新信息应覆盖旧信息。"我现在主要找什么岗位?"→AI Agent应用开发。' },
  { offset: 6,  day: 'Day31', title: 'Agent Workflow (LangGraph)', note: '阶段: Agent Workflow | 核心目标: 完整Agent Loop。使用LangGraph整理Perception→Intent→Context→Planner→Executor→Decision→Tool→Observation→Reflection→Final Answer。解决6个问题: Planner重复调用/Tool重复执行/Decision判断错误/Plan执行不完整/无限循环/Premature Stop。设计10个复杂任务验收。' },
  { offset: 7,  day: 'Day32', title: 'UC03 JD Analysis', note: '阶段: UC03 | 核心目标: JD分析完整闭环。第一版文本粘贴JD, 输出基本信息(岗位/公司/城市/薪资/学历/经验/地点)、技能、Agent分析(岗位匹配度/优势/差距/建议补充/是否建议投递)。验收(Milestone 2): 粘贴JD→点击分析→Agent分析→页面展示。' },
  { offset: 8,  day: 'Day33', title: 'UC04 Skill Gap', note: '阶段: UC04 | 核心目标: Skill Gap。流程: Resume+JD→Skill Extraction→Skill Matching→Gap Analysis。输出: 已掌握/部分掌握/缺失, 增加优先级P0必须补/P1建议补/P2了解即可。' },
  { offset: 9,  day: 'Day34', title: 'UC05 面试知识库', note: '阶段: UC05 | 核心目标: 面试知识库。处理抖音笔记: Agent识别是否面试内容, 提取问题/答案/技术领域/难度/来源, 存入RAG。DayLoop展示"今日新增面试题: 12", 按分类可点击查看。' },
  { offset: 10, day: 'Day35', title: 'UC06 模拟面试', note: '阶段: UC06 | 核心目标: 模拟面试。Text Interview, 第一版只做JD针对性面试。Agent读取Resume/Skills/Target/JD/Knowledge/Memory→生成题目→用户回答→分析→追问→再回答。输出: 得分/优点/不足/遗漏知识/建议。验收(Milestone 3): 至少连续5轮。' },
  { offset: 11, day: 'Day36', title: 'UC01/UC02 行业+招聘信息', note: '阶段: UC01/02 | 核心目标: 行业/招聘信息。UC01: 定时任务→数据源(ByteDance/Alibaba/Tencent/Anthropic/GitHub)→抓取→LLM总结→保存→DayLoop展示。UC02: 岗位+城市+职位+技能→Agent判断相关性/匹配度/投递建议。验收: 首页展示今日AI动态/今日新增招聘/今日新增面试题。' },
  { offset: 12, day: 'Day37', title: '自动化 + API工程化', note: '阶段: 自动化 & API | 核心目标: 定时任务、数据同步。禁止新增功能。统一API: /chat /jd/analyze /skills/gap /interview/start /interview/answer /news /jobs /interview/questions。统一Request/Response/Error/Exception/Logging, 增加Request ID/Session ID/User ID/Trace ID。' },
  { offset: 13, day: 'Day38', title: 'Evaluation系统化评测', note: '阶段: Evaluation | 核心目标: 系统化评测。禁止新增功能。建立Evaluation Dataset: Intent 20/JD 20/RAG 20/Tool 20/Memory 10/Interview 10 = 100 Case。指标: Agent(Task Success/Intent Accuracy/Tool Accuracy), RAG(Hit@5/Faithfulness/Answer Relevance), JD(Field/Skill/Gap), Interview(Question/Answer/Follow-up质量), API(Success Rate/P95/Error Rate)。' },
  { offset: 14, day: 'Day39', title: '全面测试+Debug+MVP Release', note: '阶段: 全面测试/调试/演示 | 核心目标: MVP Release。禁止新增功能。上午功能测试(登录/AI Chat/Context/Tool/RAG/Memory/JD分析/Skill Gap/面试知识库/模拟面试/行业信息/招聘), 中午异常测试(LLM Timeout/API Timeout/Tool Error/RAG无结果/数据库异常/参数错误/空输入/超长输入), 下午E2E 2个场景, 晚上最终Demo。' },
];

const stmt = db.prepare(`
  INSERT INTO tasks (date, title, start_time, end_time, planned_duration, category, priority, note, is_recurring, is_planned, achievement, note_id, sync_enabled, planned_days, overall_status, user_id)
  VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0, 1, '', NULL, 1, 1, 'pending', ?)
`);

const start = new Date('2026-08-14');
const tx = db.transaction((rows) => {
  for (const d of rows) {
    const date = start.toISOString().slice(0, 10);
    start.setDate(start.getDate() + 1);
    const exists = db.prepare('SELECT id FROM tasks WHERE user_id = ? AND date = ? AND title = ?').get(USER_ID, date, d.title);
    if (exists) {
      console.log(`SKIP  ${date} ${d.day} ${d.title} (已存在)`);
      continue;
    }
    stmt.run(date, `${d.day} ${d.title}`, START, END, 570, CATEGORY, PRIORITY, d.note, USER_ID);
    console.log(`INSERT  ${date} ${d.day} ${d.title}`);
  }
});
tx(days);
console.log('完成');