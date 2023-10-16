ps_4_0
dcl_input_ps linear v0.xy
dcl_output o0
deriv_rtx o0.x, v0.x
deriv_rty o0.y, v0.y
mov o0.zw, l(0, 0, 1, 0)
ret
