ps_1_2
tex t0
texdp3 t1, t0_bx2
texm3x2pad t2, t0_bx2
texm3x2tex t3, t0_bx2
mad r0, t1, t3, v0
