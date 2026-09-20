ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
deriv_rtx_coarse r0.x, v0.x
deriv_rty_coarse r0.y, v0.y
deriv_rtx_fine r0.z, v0.x
deriv_rty_fine r0.w, v0.y
rcp r1.x, v0.x
add o0, r0, r1.x
ret
