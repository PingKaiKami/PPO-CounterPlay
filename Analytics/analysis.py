import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import seaborn as sns
from scipy.stats import entropy
import os

plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei'] 
plt.rcParams['axes.unicode_minus'] = False

# --- 1. 定義檔案路徑 (將 model_65 對應到 BC+GAIL 標籤) ---
paths = {
    "Human": "Human_BattleData.csv",
    "Model_BC": "model_BC/AI_BattleData.csv",
    "Model_GAIL": "model_GAIL/AI_BattleData.csv",
    "BC+GAIL": "model_65/AI_BattleData.csv"
}

# --- 2. 載入資料並進行防錯檢查 ---
data = {}
for name, path in paths.items():
    if os.path.exists(path):
        data[name] = pd.read_csv(path)
        print(f"✅ 成功載入 {name} 數據 (共 {len(data[name])} 筆)")
    else:
        print(f"⚠️ 警告：找不到 {name} 的檔案，路徑為 '{path}'")

if "Human" not in data or len(data) < 2:
    print("❌ 錯誤：必須包含 Human 資料與至少一組 AI 資料才能進行分析！")
    exit()

# --- 3. 計算全域級距 (確保所有子圖座標軸一致) ---
all_dist = []
all_strafe = []
all_angvel = []
for df in data.values():
    all_dist.append(df['Distance'].max())
    all_strafe.append(df['StrafingIntensity'].max())
    all_angvel.append(df['AngularVelocity'].max())

max_dist = max(all_dist) * 1.1
max_strafe = 1.1 
max_angvel_limit = min(max(all_angvel), 40) 

# --- 4. 繪製 2x4 矩陣熱點圖 (2 橫排, 4 直列) ---
sns.set_theme(style="whitegrid")
plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei'] 

fig, axes = plt.subplots(2, 4, figsize=(24, 12))

cmaps = {
    "Human": "Blues",
    "Model_BC": "Oranges",
    "Model_GAIL": "Greens",
    "BC+GAIL": "Purples"
}

model_order = ["Human", "Model_BC", "Model_GAIL", "BC+GAIL"]

for col_idx, name in enumerate(model_order):
    if name not in data:
        axes[0, col_idx].text(0.5, 0.5, f"No {name} Data", ha='center', va='center', fontsize=28)
        axes[1, col_idx].text(0.5, 0.5, f"No {name} Data", ha='center', va='center', fontsize=28)
        continue
    
    df = data[name]
    cmap = cmaps[name]
    kde_params = dict(fill=True, thresh=0, levels=25, cmap=cmap)

    # 上排：走位邏輯 (Distance vs Strafing)
    sns.kdeplot(data=df, x="Distance", y="StrafingIntensity", ax=axes[0, col_idx], **kde_params)
    axes[0, col_idx].set_title(f"{name}\nDistance vs Strafing", fontsize=18, fontweight='bold')
    axes[0, col_idx].set_xlim(0, max_dist)
    axes[0, col_idx].set_ylim(-0.1, max_strafe)

    # 下排：攻擊強度 (Distance vs Attack Strength)
    sns.kdeplot(data=df, x="Distance", y="AngularVelocity", ax=axes[1, col_idx], **kde_params)
    axes[1, col_idx].set_title(f"{name}\nDistance vs Attack Strength", fontsize=18, fontweight='bold')
    axes[1, col_idx].set_xlim(0, max_dist)
    axes[1, col_idx].set_ylim(-1, max_angvel_limit)

plt.tight_layout()
plt.savefig('Battle_Heatmaps_Comparison.png')
print("\n✅ 四方對比熱點圖已儲存為 'Battle_Heatmaps_Comparison.png'")

# --- 5. 計算 KL 散度對比 ---
def calculate_kl(p_data, q_data, bins=50):
    p_hist, _ = np.histogram(p_data, bins=bins, range=(p_data.min(), p_data.max()), density=True)
    q_hist, _ = np.histogram(q_data, bins=bins, range=(p_data.min(), p_data.max()), density=True)
    p_hist = p_hist + 1e-10
    q_hist = q_hist + 1e-10
    return entropy(p_hist, q_hist)

print("\n================ 模仿學習量化指標 (KL Divergence) ================")
df_h = data["Human"]
for name in ["Model_BC", "Model_GAIL", "BC+GAIL"]:
    if name in data:
        df_target = data[name]
        kl_strafe = calculate_kl(df_h['StrafingIntensity'], df_target['StrafingIntensity'])
        kl_attack = calculate_kl(df_h['AngularVelocity'], df_target['AngularVelocity'])
        print(f"【{name}】 側移 KLD: {kl_strafe:.4f} | 攻擊 KLD: {kl_attack:.4f} (越接近 0 越像人)")

# --- 6. 戰術反擊分析 (Reaction Time) ---
# 💡 簡化：不再處理 -3.0，只處理 -2.0(無效對局/冷卻) 與 -1.0(Miss) 
def analyze_reaction_data(df):
    # 直接過濾掉所有 -2.0 (包含了無事件、以及因為全程冷卻而被忽略的對局)
    valid_events = df[df['ReactionTime'] != -2.0]
    total_valid = len(valid_events)
    
    # 成功反擊的次數 (時間 > 0)
    successes = valid_events[valid_events['ReactionTime'] > 0.0]
    num_success = len(successes)
    
    # 失敗次數 (時間為 -1.0)
    num_miss = len(valid_events[valid_events['ReactionTime'] == -1.0])
    
    # 計算成功率
    success_rate = (num_success / total_valid * 100.0) if total_valid > 0 else 0.0
    avg_reaction_time = successes['ReactionTime'].mean() if num_success > 0 else 0.0
    
    return {
        "valid_total": total_valid,
        "success": num_success,
        "miss": num_miss,
        "rate": success_rate,
        "avg_time": avg_reaction_time,
        "success_times": successes['ReactionTime'].tolist()
    }

stats = {}
for name, df in data.items():
    stats[name] = analyze_reaction_data(df)

print("\n================ 戰術反擊指標 (Counter-Attack) ================")
for name, s in stats.items():
    print(f"【{name}】 有效機會: {s['valid_total']} | 成功: {s['success']} | 失敗: {s['miss']} | 成功率: {s['rate']:.1f}% | 平均反應時間: {s['avg_time']:.3f} 秒")

# --- 7. 戰術分析視覺化圖表 ---
fig_tac, axes_tac = plt.subplots(1, 2, figsize=(15, 6))

# 💡 定義全域統一的顏色映射 (100% 確保所有圖表顏色對齊)
color_palette = {
    "Human": "#3498db",      # 藍色
    "Model_BC": "#95a5a6",   # 灰色
    "Model_GAIL": "#e67e22", # 橘色
    "BC+GAIL": "#2ecc71"     # 綠色
}

active_names = [n for n in model_order if n in data]
rates = [stats[n]['rate'] for n in active_names]

# A. 反擊衝刺的成功率直方圖 (左圖)
sns.barplot(x=active_names, y=rates, palette=color_palette, ax=axes_tac[0])
axes_tac[0].set_title("反擊衝刺的成功率 (%)", fontsize=28, fontweight='bold')
axes_tac[0].set_xlabel("模型", fontsize=16)
axes_tac[0].set_ylabel("成功率 (%)", fontsize=20)
axes_tac[0].tick_params(axis='x', labelsize=20)
axes_tac[0].set_ylim(0, 100)
for bar in axes_tac[0].patches:
    axes_tac[0].annotate(f"{bar.get_height():.1f}%", 
                         (bar.get_x() + bar.get_width() / 2, bar.get_height() - 8),
                         ha='center', va='center', color='white', fontweight='bold', fontsize=24)

# B. 反應時間分佈箱鬚圖 (右圖)
box_data = []
box_groups = []
for name in active_names:
    box_data.extend(stats[name]['success_times'])
    box_groups.extend([name] * len(stats[name]['success_times']))

reaction_df = pd.DataFrame({'Reaction Time (s)': box_data, 'Model': box_groups})

if not reaction_df.empty:
    # 💡 這裡同樣傳入 color_palette，即使 Model_BC 沒出現，GAIL 與 BC+GAIL 的顏色也絕對不會錯位
    sns.boxplot(data=reaction_df, x='Model', y='Reaction Time (s)', palette=color_palette, ax=axes_tac[1])
    sns.stripplot(data=reaction_df, x='Model', y='Reaction Time (s)', color="black", alpha=0.3, jitter=0.2, ax=axes_tac[1])
    axes_tac[1].set_title("手動衝刺反應時間分佈 (秒)", fontsize=28, fontweight='bold')
    axes_tac[1].set_xlabel("模型", fontsize=16)
    axes_tac[1].set_ylabel("反應時間 (秒)", fontsize=20)
    axes_tac[1].tick_params(axis='x', labelsize=20)
    axes_tac[1].set_ylim(0, 1.1)
else:
    axes_tac[1].text(0.5, 0.5, "無成功數據可繪製", ha='center', va='center', fontsize=28)

plt.tight_layout()
plt.subplots_adjust(wspace=0.2)
plt.savefig('Tactical_Analysis_Comparison.png')
print("✅ 戰術分析圖表已儲存為 'Tactical_Analysis_Comparison.png'")

# --- 8. 多線重疊：距離分佈圖 ---
plt.figure(figsize=(12, 7))
for name in active_names:
    color = {"Human": "#3498db", "Model_BC": "#e67e22", "Model_GAIL": "#2ecc71", "BC+GAIL": "#9b59b6"}[name]
    sns.kdeplot(data[name]['Distance'], label=name, shade=True, color=color, alpha=0.15)

plt.title("敵我距離分佈重疊圖 (所有模型對比)", fontsize=28, fontweight='bold')
plt.xlabel("敵我距離 (公尺)", fontsize=20)
plt.ylabel("數據密度", fontsize=20)
plt.legend()
plt.savefig('Distance_Overlap_Comparison.png')
plt.show()