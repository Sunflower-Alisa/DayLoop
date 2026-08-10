using Microsoft.Data.Sqlite;
using System.Text.Json;
using DayLoop.Api.Data;

namespace DayLoop.Api.Services;

public static class SeedData
{
    public static void Seed()
    {
        using var conn = Database.CreateConnection();

        long bookId = EnsureWordBook(conn);
        SeedWords(conn, bookId);
        SeedScenarios(conn);
        SeedSpeaking(conn);
        SeedClips(conn);
    }

    private static long EnsureWordBook(SqliteConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id FROM word_books WHERE is_default = 1 LIMIT 1";
            var found = cmd.ExecuteScalar();
            if (found != null) return (long)found;
        }
        using var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO word_books (name, level, description, cover_color, is_default, user_id) VALUES (@n, @l, @d, @c, 1, 0)";
        ins.Parameters.AddWithValue("@n", "核心 500 词");
        ins.Parameters.AddWithValue("@l", "beginner");
        ins.Parameters.AddWithValue("@d", "日常高频核心词汇，内置样例，快速上手");
        ins.Parameters.AddWithValue("@c", "#4f46e5");
        ins.ExecuteNonQuery();
        ins.CommandText = "SELECT last_insert_rowid()";
        return (long)ins.ExecuteScalar()!;
    }

    private static void SeedWords(SqliteConnection conn, long bookId)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM words WHERE book_id = @b";
        check.Parameters.AddWithValue("@b", bookId);
        if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

        var words = new (string w, string ph, string pos, string m, string en, string cn)[]
        {
            ("apple", "/ˈæpl/", "n.", "苹果", "An apple a day keeps the doctor away.", "一天一苹果，医生远离我。"),
            ("water", "/ˈwɔːtər/", "n.", "水", "Please give me a glass of water.", "请给我一杯水。"),
            ("friend", "/frend/", "n.", "朋友", "She is my best friend.", "她是我最好的朋友。"),
            ("family", "/ˈfæməli/", "n.", "家庭；家人", "My family lives in Shanghai.", "我的家人住在上海。"),
            ("travel", "/ˈtrævl/", "v./n.", "旅行", "I love to travel around the world.", "我喜欢环游世界。"),
            ("learn", "/lɜːrn/", "v.", "学习", "We learn English every day.", "我们每天学英语。"),
            ("success", "/səkˈses/", "n.", "成功", "Hard work leads to success.", "努力带来成功。"),
            ("smile", "/smaɪl/", "v./n.", "微笑", "She smiled at me warmly.", "她对我温暖地微笑。"),
            ("dream", "/driːm/", "n./v.", "梦想；做梦", "Follow your dream.", "追随你的梦想。"),
            ("knowledge", "/ˈnɑːlɪdʒ/", "n.", "知识", "Knowledge is power.", "知识就是力量。"),
            ("opportunity", "/ˌɑːpərˈtuːnəti/", "n.", "机会", "This is a great opportunity.", "这是一个绝佳的机会。"),
            ("courage", "/ˈkɜːrɪdʒ/", "n.", "勇气", "It takes courage to try.", "尝试需要勇气。"),
            ("explore", "/ɪkˈsplɔːr/", "v.", "探索", "Let's explore the city.", "让我们探索这座城市。"),
            ("nature", "/ˈneɪtʃər/", "n.", "自然；大自然", "We should protect nature.", "我们应该保护大自然。"),
            ("achieve", "/əˈtʃiːv/", "v.", "实现；达成", "You can achieve anything.", "你可以实现任何事。"),
            ("purpose", "/ˈpɜːrpəs/", "n.", "目的；目标", "What is your purpose?", "你的目的是什么？"),
            ("wisdom", "/ˈwɪzdəm/", "n.", "智慧", "Wisdom comes with experience.", "智慧来自经验。"),
            ("quiet", "/ˈkwaɪət/", "adj.", "安静的", "The library is very quiet.", "图书馆非常安静。"),
            ("gentle", "/ˈdʒentl/", "adj.", "温柔的", "She has a gentle voice.", "她的声音很温柔。"),
            ("green", "/ɡriːn/", "adj./n.", "绿色的；绿色", "The grass is green.", "草是绿色的。"),
        };

        using var tx = conn.BeginTransaction();
        foreach (var w in words)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO words (word, phonetic, pos, meaning, example_en, example_cn, book_id) VALUES (@w,@ph,@pos,@m,@en,@cn,@b)";
            cmd.Parameters.AddWithValue("@w", w.w);
            cmd.Parameters.AddWithValue("@ph", w.ph);
            cmd.Parameters.AddWithValue("@pos", w.pos);
            cmd.Parameters.AddWithValue("@m", w.m);
            cmd.Parameters.AddWithValue("@en", w.en);
            cmd.Parameters.AddWithValue("@cn", w.cn);
            cmd.Parameters.AddWithValue("@b", bookId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static void SeedScenarios(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM scenarios";
        if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

        var scenarios = new (string title, string cat, int level, string icon, string desc, (string sp, string en, string cn)[] lines, (string ph, string mean, string en, string cn)[] phrases, (string qen, string qcn, string[] opts, int ans, string exp)[] quizzes)[]
        {
            (
                "机场问路", "travel", 1, "✈️", "在机场询问 information 柜台、找登机口、问路。",
                new (string, string, string)[]
                {
                    ("Passenger", "Excuse me, where is the check-in counter?", "打扰一下，值机柜台在哪里？"),
                    ("Staff", "It's over there, next to the information desk.", "在那边的信息台旁边。"),
                    ("Passenger", "Thanks. And which gate is for Flight CA183?", "谢谢。CA183 航班在几号登机口？"),
                    ("Staff", "Gate 12, at the end of the corridor.", "12 号登机口，在走廊尽头。"),
                },
                new (string, string, string, string)[]
                {
                    ("Excuse me, where is ...?", "打扰一下，请问……在哪里？", "Excuse me, where is the restroom?", "打扰一下，请问洗手间在哪里？"),
                    ("How can I get to ...?", "我怎么去……？", "How can I get to the boarding gate?", "我怎么去登机口？"),
                },
                new (string, string, string[], int, string)[]
                {
                    ("At the airport, you want to ask where the counter is. What do you say?", "在机场你想问柜台在哪，该怎么说？", new[]{ "Where is the counter?", "I am tired.", "Good morning.", "See you later." }, 0, "询问位置直接用 Where is ...? 结构。"),
                }
            ),
            (
                "咖啡馆点单", "daily", 1, "☕", "在咖啡馆点单、询问推荐、结账。",
                new (string, string, string)[]
                {
                    ("Barista", "Good morning! What can I get for you?", "早上好！请问需要点什么？"),
                    ("Customer", "I'd like a latte, please. With oat milk.", "我要一杯拿铁，谢谢。加燕麦奶。"),
                    ("Barista", "Sure. Anything else?", "好的。还要别的吗？"),
                    ("Customer", "No, that's all. How much is it?", "不用了，就这些。多少钱？"),
                    ("Barista", "That'll be six dollars.", "一共六美元。"),
                },
                new (string, string, string, string)[]
                {
                    ("What can I get for you?", "请问需要点什么？", "What can I get for you?", "请问需要点什么？"),
                    ("I'd like ... please.", "我想要……谢谢。", "I'd like a coffee, please.", "我想要一杯咖啡。"),
                },
                new (string, string, string[], int, string)[]
                {
                    ("The barista asks what you want. What's the best reply?", "店员问你需要什么，最好的回答是？", new[]{ "I'm fine.", "I'd like a latte, please.", "How are you?", "Good morning." }, 1, "点单用 I'd like ... please."),
                }
            ),
            (
                "初识开口", "daily", 2, "🙋", "第一次见面做自我介绍与寒暄。",
                new (string, string, string)[]
                {
                    ("A", "Hi! I'm Tom. Nice to meet you.", "嗨，我是汤姆。很高兴认识你。"),
                    ("B", "Nice to meet you too, Tom. I'm Lucy.", "我也是，汤姆。我是露西。"),
                    ("A", "Where are you from?", "你来自哪里？"),
                    ("B", "I'm from Beijing. What about you?", "我来自北京。你呢？"),
                },
                new (string, string, string, string)[]
                {
                    ("Nice to meet you.", "很高兴认识你。", "Nice to meet you.", "很高兴认识你。"),
                    ("Where are you from?", "你来自哪里？", "Where are you from?", "你来自哪里？"),
                },
                new (string, string, string[], int, string)[]
                {
                    ("Your friend says 'Nice to meet you'. How do you respond?", "你的朋友说‘很高兴认识你’，你如何回应？", new[]{ "Nice to meet you too.", "Thank you.", "I'm fine.", "Goodbye." }, 0, "用 Nice to meet you too. 回应。" ),
                }
            ),
        };

        using var tx = conn.BeginTransaction();
        foreach (var s in scenarios)
        {
            using var sc = conn.CreateCommand();
            sc.CommandText = "INSERT INTO scenarios (title, category, level, icon, description, user_id) VALUES (@t, @c, @l, @i, @d, 0)";
            sc.Parameters.AddWithValue("@t", s.title);
            sc.Parameters.AddWithValue("@c", s.cat);
            sc.Parameters.AddWithValue("@l", s.level);
            sc.Parameters.AddWithValue("@i", s.icon);
            sc.Parameters.AddWithValue("@d", s.desc);
            sc.ExecuteNonQuery();
            sc.CommandText = "SELECT last_insert_rowid()";
            var sid = (long)sc.ExecuteScalar()!;

            for (var i = 0; i < s.lines.Length; i++)
            {
                using var l = conn.CreateCommand();
                l.CommandText = "INSERT INTO scenario_lines (scenario_id, ord, speaker, en_text, cn_text) VALUES (@s, @o, @sp, @en, @cn)";
                l.Parameters.AddWithValue("@s", sid);
                l.Parameters.AddWithValue("@o", i);
                l.Parameters.AddWithValue("@sp", s.lines[i].sp);
                l.Parameters.AddWithValue("@en", s.lines[i].en);
                l.Parameters.AddWithValue("@cn", s.lines[i].cn);
                l.ExecuteNonQuery();
            }
            foreach (var p in s.phrases)
            {
                using var ph = conn.CreateCommand();
                ph.CommandText = "INSERT INTO scenario_phrases (scenario_id, phrase, meaning, example_en, example_cn) VALUES (@s, @p, @m, @en, @cn)";
                ph.Parameters.AddWithValue("@s", sid);
                ph.Parameters.AddWithValue("@p", p.ph);
                ph.Parameters.AddWithValue("@m", p.mean);
                ph.Parameters.AddWithValue("@en", p.en);
                ph.Parameters.AddWithValue("@cn", p.cn);
                ph.ExecuteNonQuery();
            }
            foreach (var q in s.quizzes)
            {
                using var qz = conn.CreateCommand();
                qz.CommandText = "INSERT INTO scenario_quizzes (scenario_id, question_en, question_cn, options, answer_index, explanation) VALUES (@s, @qe, @qc, @op, @ai, @ex)";
                qz.Parameters.AddWithValue("@s", sid);
                qz.Parameters.AddWithValue("@qe", q.qen);
                qz.Parameters.AddWithValue("@qc", q.qcn);
                qz.Parameters.AddWithValue("@op", JsonSerializer.Serialize(q.opts));
                qz.Parameters.AddWithValue("@ai", q.ans);
                qz.Parameters.AddWithValue("@ex", q.exp);
                qz.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    private static void SeedSpeaking(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM speaking_topics";
        if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

        var topics = new (string title, string cat, string level, string tsource, long sid, (string en, string cn, string au)[] lines)[]
        {
            ("自我介绍", "daily", "beginner", "topic", 0, new (string, string, string)[]
            {
                ("Hi, my name is Zhang Wei.", "你好，我叫张伟。", ""),
                 ("I'm a software engineer from Beijing.", "我是来自北京的软件工程师。", ""),
                 ("I enjoy learning new things every day.", "我喜欢每天学习新东西。", ""),
            }),
            ("点餐练习", "scenario", "beginner", "scenario", 2, new (string, string, string)[]
            {
                ("Good morning! What can I get for you?", "早上好！请问需要什么？", ""),
                ("I'd like a latte with sugar, please.", "请给我一杯加糖的拿铁。", ""),
                ("How much is it?", "多少钱？", ""),
            }),
        };

        foreach (var t in topics)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO speaking_topics (title, category, level, lines, source_type, source_id, user_id) VALUES (@t, @c, @lv, @l, @st, @si, 0)";
            ins.Parameters.AddWithValue("@t", t.title);
            ins.Parameters.AddWithValue("@c", t.cat);
            ins.Parameters.AddWithValue("@lv", t.level);
            ins.Parameters.AddWithValue("@l", JsonSerializer.Serialize(t.lines.Select(x => new { en = x.en, cn = x.cn, audio_url = x.au }).ToList()));
            ins.Parameters.AddWithValue("@st", t.tsource);
            ins.Parameters.AddWithValue("@si", t.sid);
            ins.ExecuteNonQuery();
        }
    }

    private static void SeedClips(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM video_clips";
        if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

        using var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO video_clips (title, source, level, tags, description, duration, user_id) VALUES ('公园散步对话', '纪录片', 'medium', '日常,对话', '一段关于公园散步的日常对话，适合中级跟读。', 45, 0)";
        ins.ExecuteNonQuery();
        ins.CommandText = "SELECT last_insert_rowid()";
        var cid = (long)ins.ExecuteScalar()!;

        var lines = new (int o, string sp, string en, string cn, double s, double e)[]
        {
            (0, "A", "What a beautiful day for a walk in the park!", "今天天气真好，适合在公园散步！", 0, 4.5),
            (1, "B", "Yes, it really is. The flowers are blooming everywhere.", "是啊，到处都是盛开的花儿。", 4.5, 9),
            (2, "A", "Would you like to grab some coffee by the lake?", "要不要去湖边喝杯咖啡？", 9, 13),
            (3, "B", "That sounds great. I know a quiet spot over there.", "听起来不错。我知道那边有个安静的好地方。", 13, 17),
        };
        foreach (var l in lines)
        {
            using var lc = conn.CreateCommand();
            lc.CommandText = "INSERT INTO clip_lines (clip_id, ord, speaker, en_text, cn_text, start_time, end_time) VALUES (@c, @o, @sp, @en, @cn, @s, @e)";
            lc.Parameters.AddWithValue("@c", cid);
            lc.Parameters.AddWithValue("@o", l.o);
            lc.Parameters.AddWithValue("@sp", l.sp);
            lc.Parameters.AddWithValue("@en", l.en);
            lc.Parameters.AddWithValue("@cn", l.cn);
            lc.Parameters.AddWithValue("@s", l.s);
            lc.Parameters.AddWithValue("@e", l.e);
            lc.ExecuteNonQuery();
        }
    }
}