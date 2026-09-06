ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
if b0
texld r0, v0.xy, s0
if b1
mul oC0, r0, c1
else
mov oC0, r0
endif
else
mov oC0, c0
endif
