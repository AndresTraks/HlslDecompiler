vs_3_0
def c25, 3, 1, 0, 0
dcl_position v0
dcl_position o0
mov r0.x, c25.x
mul r0.x, r0.x, c24.x
mova a0.x, r0.x
mov r0, c2[a0.x]
mov r1.x, c1[a0.x].x
mad r1.xyz, r0.xyz, r1.xxx, c0[a0.x].xyz
mad r1.w, r0.w, c1[a0.x].x, v0.w
mad r0, v0.xyzx, c25.yyyz, c25.zzzy
add o0, r0, r1
