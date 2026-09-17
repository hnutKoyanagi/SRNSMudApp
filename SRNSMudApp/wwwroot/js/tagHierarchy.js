/**
 * Chrome Built-in AI (Prompt API / Gemini Nano) を使用してタグの階層・包含関係を判定するモジュール
 */
export async function determineHierarchyLocal(candidateTags) {
  if (!window.ai || !window.ai.languageModel) {
    throw new Error("Chrome Prompt API is not supported on this browser.");
  }

  const capabilities = await window.ai.languageModel.capabilities();
  if (capabilities.available === "no") {
    throw new Error("Model is not ready or not downloaded.");
  }

  const session = await window.ai.languageModel.create({
    systemPrompt: "あなたは概念の包含関係を分析する分類器です。与えられたタグ候補の中でどれが最上位の概念かを判定し、JSON形式 { \"root\": \"...\", \"parent_of\": { \"子タグ\": \"親タグ\" } } のみを出力してください。"
  });

  try {
    const prompt = `タグ候補: ${JSON.stringify(candidateTags)}`;
    const resultText = await session.prompt(prompt);

    // Markdownコードブロック（```json ... ```）の除去
    let cleaned = resultText.trim();
    if (cleaned.startsWith("```json")) {
      cleaned = cleaned.substring(7);
    } else if (cleaned.startsWith("```")) {
      cleaned = cleaned.substring(3);
    }
    if (cleaned.endsWith("```")) {
      cleaned = cleaned.substring(0, cleaned.length - 3);
    }
    cleaned = cleaned.trim();

    return JSON.parse(cleaned);
  } finally {
    if (session && typeof session.destroy === "function") {
      session.destroy();
    }
  }
}

