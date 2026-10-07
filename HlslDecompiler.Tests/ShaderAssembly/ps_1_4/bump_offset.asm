ps_1_4
texld r1, t1
texcrd r2.xy, t2.xy
bem r2.xy, r2.xy, r1.xy
phase
texld r2, r2
texld r3, t3
mul r0, r2, v0
add r0, r0, r3
