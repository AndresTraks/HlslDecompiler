ds_5_0
dcl_input_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_globalFlags refactoringAllowed
dcl_input vpc0.y
dcl_input_siv vpc1.x, finalQuadVeq0EdgeTessFactor
dcl_input_siv vpc3.x, finalQuadVeq1EdgeTessFactor
dcl_input_siv vpc4.x, finalQuadUInsideTessFactor
dcl_input vDomain.xy
dcl_input vicp[4][0]
dcl_output_siv o0, position
dcl_temps 2
add r0, -vicp[2][0], vicp[3][0]
mad r0, vDomain.x, r0, vicp[2][0]
add r1, -vicp[0][0], vicp[1][0]
mad r1, vDomain.x, r1, vicp[0][0]
add r0, r0, -r1
mad r0, vDomain.y, r0, r1
and r1.x, vpc0.y, l(0x00000001)
mul r1.y, vpc1.x, vpc4.x
movc r1.x, r1.x, r1.y, vpc0.y
mad o0, r0, r1.x, vpc3.x
ret
