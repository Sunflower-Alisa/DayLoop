const db = require('./database');

function seed() {
  seedWordBook();
  seedWords();
  seedScenarios();
  seedSpeaking();
  seedClips();
}

function seedWordBook() {
  const found = db.prepare('SELECT id FROM word_books WHERE is_default = 1 LIMIT 1').get();
  if (found) return;
  db.prepare(
    `INSERT INTO word_books (name, level, description, cover_color, is_default, user_id)
     VALUES ('核心 500 词', 'beginner', '日常高频核心词汇，内置样例，快速上手', '#4f46e5', 1, 0)`
  ).run();
}

function seedWords() {
  const book = db.prepare('SELECT id FROM word_books WHERE is_default = 1 LIMIT 1').get();
  if (!book) return;
  const count = db.prepare('SELECT COUNT(*) as c FROM words WHERE book_id = ?').get(book.id);
  if (count.c > 0) return;

  const words = [
    ['apple', '/ˈæpl/', 'n.', '苹果', 'An apple a day keeps the doctor away.', '一天一苹果，医生远离我。'],
    ['water', '/ˈwɔːtər/', 'n.', '水', 'Please give me a glass of water.', '请给我一杯水。'],
    ['friend', '/frend/', 'n.', '朋友', 'She is my best friend.', '她是我最好的朋友。'],
    ['family', '/ˈfæməli/', 'n.', '家庭；家人', 'My family lives in Shanghai.', '我的家人住在上海。'],
    ['travel', '/ˈtrævl/', 'v./n.', '旅行', 'I love to travel around the world.', '我喜欢环游世界。'],
    ['learn', '/lɜːrn/', 'v.', '学习', 'We learn English every day.', '我们每天学英语。'],
    ['success', '/səkˈses/', 'n.', '成功', 'Hard work leads to success.', '努力带来成功。'],
    ['smile', '/smaɪl/', 'v./n.', '微笑', 'She smiled at me warmly.', '她对我温暖地微笑。'],
    ['dream', '/driːm/', 'n./v.', '梦想；做梦', 'Follow your dream.', '追随你的梦想。'],
    ['knowledge', '/ˈnɑːlɪdʒ/', 'n.', '知识', 'Knowledge is power.', '知识就是力量。'],
    ['opportunity', '/ˌɑːpərˈtuːnəti/', 'n.', '机会', 'This is a great opportunity.', '这是一个绝佳的机会。'],
    ['courage', '/ˈkɜːrɪdʒ/', 'n.', '勇气', 'It takes courage to try.', '尝试需要勇气。'],
    ['explore', '/ɪkˈsplɔːr/', 'v.', '探索', "Let's explore the city.", '让我们探索这座城市。'],
    ['nature', '/ˈneɪtʃər/', 'n.', '自然；大自然', 'We should protect nature.', '我们应该保护大自然。'],
    ['achieve', '/əˈtʃiːv/', 'v.', '实现；达成', 'You can achieve anything.', '你可以实现任何事。'],
    ['purpose', '/ˈpɜːrpəs/', 'n.', '目的；目标', 'What is your purpose?', '你的目的是什么？'],
    ['wisdom', '/ˈwɪzdəm/', 'n.', '智慧', 'Wisdom comes with experience.', '智慧来自经验。'],
    ['quiet', '/ˈkwaɪət/', 'adj.', '安静的', 'The library is very quiet.', '图书馆非常安静。'],
    ['gentle', '/ˈdʒentl/', 'adj.', '温柔的', 'She has a gentle voice.', '她的声音很温柔。'],
    ['green', '/ɡriːn/', 'adj./n.', '绿色的；绿色', 'The grass is green.', '草是绿色的。'],
  ];

  const stmt = db.prepare(
    `INSERT INTO words (word, phonetic, pos, meaning, example_en, example_cn, book_id) VALUES (?, ?, ?, ?, ?, ?, ?)`
  );
  const tx = db.transaction((rows) => {
    for (const w of rows) stmt.run(w[0], w[1], w[2], w[3], w[4], w[5], book.id);
  });
  tx(words);
}

function seedScenarios() {
  const count = db.prepare('SELECT COUNT(*) as c FROM scenarios').get();
  if (count.c > 0) return;

  const scenarios = [
    {
      title: '机场问路', category: 'travel', level: 1, icon: '✈️',
      description: '在机场询问 information 柜台、找登机口、问路。',
      lines: [
        ['Passenger', 'Excuse me, where is the check-in counter?', '打扰一下，值机柜台在哪里？'],
        ['Staff', "It's over there, next to the information desk.", '在那边的信息台旁边。'],
        ['Passenger', 'Thanks. And which gate is for Flight CA183?', '谢谢。CA183 航班在几号登机口？'],
        ['Staff', 'Gate 12, at the end of the corridor.', '12 号登机口，在走廊尽头。'],
      ],
      phrases: [
        ['Excuse me, where is ...?', '打扰一下，请问……在哪里？', 'Excuse me, where is the restroom?', '打扰一下，请问洗手间在哪里？'],
        ['How can I get to ...?', '我怎么去……？', 'How can I get to the boarding gate?', '我怎么去登机口？'],
      ],
      quizzes: [
        ['At the airport, you want to ask where the counter is. What do you say?', '在机场你想问柜台在哪，该怎么说？', ['Where is the counter?', 'I am tired.', 'Good morning.', 'See you later.'], 0, '询问位置直接用 Where is ...? 结构。'],
      ],
    },
    {
      title: '咖啡馆点单', category: 'daily', level: 1, icon: '☕',
      description: '在咖啡馆点单、询问推荐、结账。',
      lines: [
        ['Barista', 'Good morning! What can I get for you?', '早上好！请问需要点什么？'],
        ['Customer', "I'd like a latte, please. With oat milk.", '我要一杯拿铁，谢谢。加燕麦奶。'],
        ['Barista', 'Sure. Anything else?', '好的。还要别的吗？'],
        ['Customer', "No, that's all. How much is it?", '不用了，就这些。多少钱？'],
        ['Barista', "That'll be six dollars.", '一共六美元。'],
      ],
      phrases: [
        ['What can I get for you?', '请问需要点什么？', 'What can I get for you?', '请问需要点什么？'],
        ["I'd like ... please.", '我想要……谢谢。', "I'd like a coffee, please.", '我想要一杯咖啡。'],
      ],
      quizzes: [
        ['The barista asks what you want. What\'s the best reply?', '店员问你需要什么，最好的回答是？', ["I'm fine.", "I'd like a latte, please.", 'How are you?', 'Good morning.'], 1, "点单用 I'd like ... please."],
      ],
    },
    {
      title: '初识开口', category: 'daily', level: 2, icon: '🙋',
      description: '第一次见面做自我介绍与寒暄。',
      lines: [
        ['A', "Hi! I'm Tom. Nice to meet you.", '嗨，我是汤姆。很高兴认识你。'],
        ['B', "Nice to meet you too, Tom. I'm Lucy.", '我也是，汤姆。我是露西。'],
        ['A', 'Where are you from?', '你来自哪里？'],
        ['B', "I'm from Beijing. What about you?", '我来自北京。你呢？'],
      ],
      phrases: [
        ['Nice to meet you.', '很高兴认识你。', 'Nice to meet you.', '很高兴认识你。'],
        ['Where are you from?', '你来自哪里？', 'Where are you from?', '你来自哪里？'],
      ],
      quizzes: [
        ['Your friend says \'Nice to meet you\'. How do you respond?', '你的朋友说‘很高兴认识你’，你如何回应？', ['Nice to meet you too.', 'Thank you.', "I'm fine.", 'Goodbye.'], 0, '用 Nice to meet you too. 回应。'],
      ],
    },
  ];

  const tx = db.transaction((rows) => {
    for (const s of rows) {
      const res = db.prepare(
        `INSERT INTO scenarios (title, category, level, icon, description, user_id) VALUES (?, ?, ?, ?, ?, 0)`
      ).run(s.title, s.category, s.level, s.icon, s.description);
      const sid = res.lastInsertRowid;
      const lineStmt = db.prepare(
        `INSERT INTO scenario_lines (scenario_id, ord, speaker, en_text, cn_text) VALUES (?, ?, ?, ?, ?)`
      );
      s.lines.forEach((l, i) => lineStmt.run(sid, i, l[0], l[1], l[2]));
      const phraseStmt = db.prepare(
        `INSERT INTO scenario_phrases (scenario_id, phrase, meaning, example_en, example_cn) VALUES (?, ?, ?, ?, ?)`
      );
      s.phrases.forEach(p => phraseStmt.run(sid, p[0], p[1], p[2], p[3]));
      const quizStmt = db.prepare(
        `INSERT INTO scenario_quizzes (scenario_id, question_en, question_cn, options, answer_index, explanation) VALUES (?, ?, ?, ?, ?, ?)`
      );
      s.quizzes.forEach(q => quizStmt.run(sid, q[0], q[1], JSON.stringify(q[2]), q[3], q[4]));
    }
  });
  tx(scenarios);
}

function seedSpeaking() {
  const count = db.prepare('SELECT COUNT(*) as c FROM speaking_topics').get();
  if (count.c > 0) return;

  const topics = [
    {
      title: '自我介绍', category: 'daily', level: 'beginner', sourceType: 'topic', sourceId: 0,
      lines: [
        { en: 'Hi, my name is Zhang Wei.', cn: '你好，我叫张伟。', audio_url: '' },
        { en: "I'm a software engineer from Beijing.", cn: '我是来自北京的软件工程师。', audio_url: '' },
        { en: 'I enjoy learning new things every day.', cn: '我喜欢每天学习新东西。', audio_url: '' },
      ],
    },
    {
      title: '点餐练习', category: 'scenario', level: 'beginner', sourceType: 'scenario', sourceId: 2,
      lines: [
        { en: 'Good morning! What can I get for you?', cn: '早上好！请问需要什么？', audio_url: '' },
        { en: "I'd like a latte with sugar, please.", cn: '请给我一杯加糖的拿铁。', audio_url: '' },
        { en: 'How much is it?', cn: '多少钱？', audio_url: '' },
      ],
    },
  ];

  const stmt = db.prepare(
    `INSERT INTO speaking_topics (title, category, level, lines, source_type, source_id, user_id) VALUES (?, ?, ?, ?, ?, ?, 0)`
  );
  const tx = db.transaction((rows) => {
    for (const t of rows) stmt.run(t.title, t.category, t.level, JSON.stringify(t.lines), t.sourceType, t.sourceId);
  });
  tx(topics);
}

function seedClips() {
  const count = db.prepare('SELECT COUNT(*) as c FROM video_clips').get();
  if (count.c > 0) return;

  const res = db.prepare(
    `INSERT INTO video_clips (title, source, level, tags, description, duration, user_id)
     VALUES ('公园散步对话', '纪录片', 'medium', '日常,对话', '一段关于公园散步的日常对话，适合中级跟读。', 45, 0)`
  ).run();
  const cid = res.lastInsertRowid;

  const lines = [
    [0, 'A', 'What a beautiful day for a walk in the park!', '今天天气真好，适合在公园散步！', 0, 4.5],
    [1, 'B', 'Yes, it really is. The flowers are blooming everywhere.', '是啊，到处都是盛开的花儿。', 4.5, 9],
    [2, 'A', 'Would you like to grab some coffee by the lake?', '要不要去湖边喝杯咖啡？', 9, 13],
    [3, 'B', 'That sounds great. I know a quiet spot over there.', '听起来不错。我知道那边有个安静的好地方。', 13, 17],
  ];
  const stmt = db.prepare(
    `INSERT INTO clip_lines (clip_id, ord, speaker, en_text, cn_text, start_time, end_time) VALUES (?, ?, ?, ?, ?, ?, ?)`
  );
  const tx = db.transaction((rows) => {
    for (const l of rows) stmt.run(cid, l[0], l[1], l[2], l[3], l[4], l[5]);
  });
  tx(lines);
}

module.exports = { seed };
