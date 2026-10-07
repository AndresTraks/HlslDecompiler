ps_1_1
tex t0
texbem t1, t0
texbeml t2, t0
texreg2gb t3, t0
mul r0, t1, t2
mad r0, t3, v0, r0
