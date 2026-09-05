ps_2_0
dcl t0.x
mul_sat r0.w, t0.x, c2.x
mov r1, c0
add r1, -r1, c1
mad r0, r0.w, r1, c0
mov oC0, r0
