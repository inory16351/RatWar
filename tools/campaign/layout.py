# 전략 지도 13×6 칸 배치 (2026-11-03 넓힘, 예전 10×4). 남(아래) = 쥐 본토, 북(위) = 고양이 제국
# 칸 = (지형, 예전 칸 번호 → 이름 키 재사용 | None = 새 이름, fx, 주요 전투)
COLS, ROWS = 13, 6
L = [
 # r0 북쪽 해안 · 제국 도시
 [("bunker",0,None,None),("bunker",None,None,None),("snow",10,None,None),("forest",3,None,None),("plains",4,None,None),("city",None,None,None),("city",None,None,None),("city",None,None,None),("plains",8,None,None),("forest",9,None,None),("snow",None,None,None),("bunker",None,None,None),("bunker",None,None,None)],
 # r1 참호선 · 수도
 [("bunker",2,None,"b5"),("snow",1,None,None),("forest",None,None,None),("plains",None,None,None),("trench",5,None,None),("trench",None,None,None),("capital",6,"capital","b7"),("trench",7,"artillery",None),("plains",None,None,None),("plains",None,None,None),("forest",None,None,None),("snow",None,None,None),("bunker",None,None,None)],
 # r2 평원 · 눈 숲
 [("snow",12,None,None),("snow",11,None,"b3"),("forest",13,None,None),("plains",14,None,None),("plains",16,None,"b4"),("plains",None,None,None),("trench",None,None,None),("plains",None,None,None),("trench",None,None,None),("plains",None,None,None),("forest",None,None,None),("forest",None,None,None),("snow",None,None,None)],
 # r3 큰 강
 [("forest",20,None,None),("river",22,None,None),("river",17,None,None),("bridge",None,None,None),("river",18,None,None),("river",19,None,None),("bridge",15,None,"b6"),("river",26,None,None),("river",None,None,None),("factory",27,"workshop",None),("river",None,None,None),("bridge",None,None,None),("river",None,None,None)],
 # r4 들 · 공업 도시
 [("forest",28,None,None),("farm",21,None,None),("farm",23,None,None),("village",34,"hospital",None),("farm",24,None,None),("farm",25,None,None),("hills",None,None,None),("farm",None,None,None),("factory",38,"workshop","b2"),("city",29,None,None),("farm",None,None,None),("hills",39,None,None),("forest",None,None,None)],
 # r5 남쪽 해안 · 하수구 사령부
 [("harbor",30,None,None),("beach",None,None,None),("village",32,"recruit",None),("hills",33,None,None),("beach",31,None,"b1"),("farm",None,None,None),("sewer",35,"hq",None),("hills",36,"artillery",None),("hills",37,None,None),("village",None,None,None),("factory",None,None,None),("beach",None,None,None),("harbor",None,None,None)],
]
