ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear sample v0
dcl_input_ps_sgv constant v1.x, sampleIndex
dcl_output o0
dcl_temps 2
eval_sample_index r0, v0, v1.x
eval_snapped r1, v0, l(1, -1, 0, 0)
add o0, r0, r1
ret
