ps_1_1
def c0, 0.300000012, 0.600000024, -1, 0
tex t0
texm3x3pad t1, t0_bx2
texm3x3pad t2, t0_bx2
texm3x3spec t3, t0_bx2, c0
mul r0, t3, v0
